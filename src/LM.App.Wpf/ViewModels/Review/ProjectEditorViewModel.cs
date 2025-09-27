#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Services.Review.Design;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

public sealed class ProjectEditorViewModel : DialogViewModelBase
{
    private readonly IReadOnlyList<ReviewStageType> _stageTypes = Enum.GetValues<ReviewStageType>();
    private readonly ObservableCollection<StageBlueprintViewModel> _stages;
    private ProjectBlueprint? _template;
    private StageBlueprintViewModel? _selectedStage;
    private string _projectName = string.Empty;
    private string? _errorMessage;
    private string _checkedEntrySummary = string.Empty;

    public ProjectEditorViewModel()
    {
        _stages = new ObservableCollection<StageBlueprintViewModel>();
        _stages.CollectionChanged += OnStagesChanged;

        AddStageCommand = new RelayCommand(AddStage, CanEdit);
        RemoveStageCommand = new RelayCommand<StageBlueprintViewModel>(RemoveStage, CanModifyStage);
        MoveStageUpCommand = new RelayCommand<StageBlueprintViewModel>(MoveStageUp, CanMoveStageUp);
        MoveStageDownCommand = new RelayCommand<StageBlueprintViewModel>(MoveStageDown, CanMoveStageDown);
        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(Cancel);
    }

    public ObservableCollection<StageBlueprintViewModel> Stages => _stages;

    public IReadOnlyList<ReviewStageType> StageTypes => _stageTypes;

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (value is null)
            {
                value = string.Empty;
            }

            if (SetProperty(ref _projectName, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string LitSearchEntryId { get; private set; } = string.Empty;

    public string LitSearchRunId { get; private set; } = string.Empty;

    public int CheckedEntryCount { get; private set; }

    public string CheckedEntrySummary
    {
        get => _checkedEntrySummary;
        private set => SetProperty(ref _checkedEntrySummary, value);
    }

    public string? HookRelativePath { get; private set; }

    public StageBlueprintViewModel? SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetProperty(ref _selectedStage, value))
            {
                RemoveStageCommand.NotifyCanExecuteChanged();
                MoveStageUpCommand.NotifyCanExecuteChanged();
                MoveStageDownCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public RelayCommand AddStageCommand { get; }

    public RelayCommand<StageBlueprintViewModel> RemoveStageCommand { get; }

    public RelayCommand<StageBlueprintViewModel> MoveStageUpCommand { get; }

    public RelayCommand<StageBlueprintViewModel> MoveStageDownCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public ProjectBlueprint? Result { get; private set; }

    public void Initialize(ProjectBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _template = blueprint;
        Result = null;

        ProjectName = blueprint.Name;
        LitSearchEntryId = blueprint.LitSearchEntryId;
        LitSearchRunId = blueprint.LitSearchRunId;
        CheckedEntryCount = blueprint.CheckedEntryIds.Count;
        HookRelativePath = blueprint.HookRelativePath;
        CheckedEntrySummary = BuildCheckedEntrySummary(blueprint.CheckedEntryIds);

        _stages.Clear();
        foreach (var stage in blueprint.Stages)
        {
            var viewModel = new StageBlueprintViewModel(stage);
            _stages.Add(viewModel);
        }

        SelectedStage = _stages.FirstOrDefault();
        ErrorMessage = null;

        AddStageCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private string BuildCheckedEntrySummary(IReadOnlyList<string> entryIds)
    {
        if (entryIds.Count == 0)
        {
            return "No entries were imported from the LitSearch run.";
        }

        var preview = entryIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Take(5)
            .ToList();

        if (preview.Count == 0)
        {
            return "No entries were imported from the LitSearch run.";
        }

        var extra = entryIds.Count - preview.Count;
        return extra > 0
            ? $"{string.Join(", ", preview)} … (+{extra} more)"
            : string.Join(", ", preview);
    }

    private bool CanEdit() => _template is not null;

    private bool CanSave() => _template is not null && !string.IsNullOrWhiteSpace(ProjectName);

    private bool CanModifyStage(StageBlueprintViewModel? stage)
    {
        return _template is not null && stage is not null && _stages.Contains(stage);
    }

    private bool CanMoveStageUp(StageBlueprintViewModel? stage)
    {
        return CanModifyStage(stage) && _stages.IndexOf(stage!) > 0;
    }

    private bool CanMoveStageDown(StageBlueprintViewModel? stage)
    {
        if (!CanModifyStage(stage))
        {
            return false;
        }

        var index = _stages.IndexOf(stage!);
        return index >= 0 && index < _stages.Count - 1;
    }

    private void AddStage()
    {
        if (_template is null)
        {
            return;
        }

        var stage = new StageBlueprint(
            $"stage-def-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}",
            $"Stage {_stages.Count + 1}",
            ReviewStageType.TitleScreening,
            primaryReviewers: 1,
            secondaryReviewers: 0,
            requiresConsensus: false,
            minimumAgreements: 0,
            escalateOnDisagreement: false);

        var viewModel = new StageBlueprintViewModel(stage);
        _stages.Add(viewModel);
        SelectedStage = viewModel;
        ErrorMessage = null;
    }

    private void RemoveStage(StageBlueprintViewModel? stage)
    {
        if (!CanModifyStage(stage))
        {
            return;
        }

        var index = _stages.IndexOf(stage!);
        _stages.RemoveAt(index);

        if (_stages.Count == 0)
        {
            SelectedStage = null;
            return;
        }

        var nextIndex = Math.Min(index, _stages.Count - 1);
        SelectedStage = _stages[nextIndex];
    }

    private void MoveStageUp(StageBlueprintViewModel? stage)
    {
        if (!CanMoveStageUp(stage))
        {
            return;
        }

        var index = _stages.IndexOf(stage!);
        _stages.Move(index, index - 1);
        SelectedStage = stage;
    }

    private void MoveStageDown(StageBlueprintViewModel? stage)
    {
        if (!CanMoveStageDown(stage))
        {
            return;
        }

        var index = _stages.IndexOf(stage!);
        _stages.Move(index, index + 1);
        SelectedStage = stage;
    }

    private void Save()
    {
        if (_template is null)
        {
            return;
        }

        ErrorMessage = null;

        var trimmedName = ProjectName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ErrorMessage = "Provide a project name.";
            return;
        }

        if (_stages.Count == 0)
        {
            ErrorMessage = "Add at least one workflow stage.";
            return;
        }

        var stageBlueprints = new List<StageBlueprint>(_stages.Count);
        foreach (var stage in _stages)
        {
            if (!stage.TryBuild(out var blueprint, out var message))
            {
                ErrorMessage = message;
                SelectedStage = stage;
                return;
            }

            stageBlueprints.Add(blueprint);
        }

        Result = _template.With(trimmedName, stageBlueprints);
        RequestClose(true);
    }

    private void Cancel()
    {
        Result = null;
        RequestClose(false);
    }

    private void OnStagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RemoveStageCommand.NotifyCanExecuteChanged();
        MoveStageUpCommand.NotifyCanExecuteChanged();
        MoveStageDownCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }
}
