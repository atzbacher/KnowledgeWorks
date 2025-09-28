#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ProjectEditorStepDescriptor[] _steps;
    private readonly ReviewTemplateOption[] _templateOptions;
    private ProjectBlueprint? _template;
    private StageBlueprintViewModel? _selectedStage;
    private string _projectName = string.Empty;
    private string? _errorMessage;
    private string _litSearchEntryId = string.Empty;
    private string _litSearchRunId = string.Empty;
    private int _checkedEntryCount;
    private string _checkedEntrySummary = string.Empty;
    private string? _hookRelativePath;
    private string _metadataNotes = string.Empty;
    private ReviewTemplateOption _selectedTemplateOption;
    private ProjectEditorStep _currentStep = ProjectEditorStep.RunAndBasics;
    private bool _isBusy;
    private Func<CancellationToken, Task<ProjectBlueprint?>>? _runReloadHandler;

    public event EventHandler<StagePreviewRequestedEventArgs>? StagePreviewRequested;

    public ProjectEditorViewModel()
    {
        _stages = new ObservableCollection<StageBlueprintViewModel>();
        _stages.CollectionChanged += OnStagesChanged;

        _steps = new[]
        {
            new ProjectEditorStepDescriptor(ProjectEditorStep.RunAndBasics, "LitSearch run", "Select a run and review template."),
            new ProjectEditorStepDescriptor(ProjectEditorStep.Metadata, "Metadata", "Capture working notes for the team."),
            new ProjectEditorStepDescriptor(ProjectEditorStep.Workflow, "Workflow", "Design the layers reviewers will work through."),
            new ProjectEditorStepDescriptor(ProjectEditorStep.Summary, "Summary", "Verify the configuration before saving.")
        };

        _templateOptions = new[]
        {
            new ReviewTemplateOption(ReviewTemplateKind.Picos, "PICO(S) review", "Screen quickly with inclusion/exclusion shortcuts."),
            new ReviewTemplateOption(ReviewTemplateKind.Custom, "Custom workflow", "Start from a blank slate and tailor each step."),
            new ReviewTemplateOption(ReviewTemplateKind.RapidAssessment, "Rapid assessment", "Optimised for consensus-driven rapid reviews."),
            new ReviewTemplateOption(ReviewTemplateKind.DataExtraction, "Data extraction", "Focus on extraction tables and structured notes.")
        };

        _selectedTemplateOption = _templateOptions[0];

        AddStageCommand = new RelayCommand(AddStage, CanEdit);
        RemoveStageCommand = new RelayCommand<StageBlueprintViewModel>(RemoveStage, CanModifyStage);
        MoveStageUpCommand = new RelayCommand<StageBlueprintViewModel>(MoveStageUp, CanMoveStageUp);
        MoveStageDownCommand = new RelayCommand<StageBlueprintViewModel>(MoveStageDown, CanMoveStageDown);
        PreviewStageCommand = new RelayCommand<StageBlueprintViewModel>(PreviewStage, CanPreviewStage);
        NextCommand = new RelayCommand(MoveNext, CanMoveNext);
        BackCommand = new RelayCommand(MoveBack, CanMoveBack);
        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(Cancel);
        ChangeRunCommand = new AsyncRelayCommand(ChangeRunAsync, CanChangeRun);
    }

    public ObservableCollection<StageBlueprintViewModel> Stages => _stages;

    public IReadOnlyList<ReviewStageType> StageTypes => _stageTypes;

    public IReadOnlyList<ProjectEditorStepDescriptor> Steps => _steps;

    public IReadOnlyList<ReviewTemplateOption> TemplateOptions => _templateOptions;

    public ProjectEditorStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                ErrorMessage = null;
                OnPropertyChanged(nameof(CurrentStepDescriptor));
                OnPropertyChanged(nameof(CurrentStepIndex));
                OnPropertyChanged(nameof(IsSummaryStep));
                UpdateNavigationState();
            }
        }
    }

    public ProjectEditorStepDescriptor CurrentStepDescriptor
    {
        get
        {
            var index = CurrentStepIndex;
            return index >= 0 ? _steps[index] : _steps[0];
        }
    }

    public int CurrentStepIndex => Array.FindIndex(_steps, descriptor => descriptor.Step == CurrentStep);

    public bool IsSummaryStep => CurrentStep == ProjectEditorStep.Summary;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                UpdateNavigationState();
            }
        }
    }

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

    public string LitSearchEntryId
    {
        get => _litSearchEntryId;
        private set => SetProperty(ref _litSearchEntryId, value);
    }

    public string LitSearchRunId
    {
        get => _litSearchRunId;
        private set => SetProperty(ref _litSearchRunId, value);
    }

    public int CheckedEntryCount
    {
        get => _checkedEntryCount;
        private set => SetProperty(ref _checkedEntryCount, value);
    }

    public string CheckedEntrySummary
    {
        get => _checkedEntrySummary;
        private set => SetProperty(ref _checkedEntrySummary, value);
    }

    public string? HookRelativePath
    {
        get => _hookRelativePath;
        private set => SetProperty(ref _hookRelativePath, value);
    }

    public ReviewTemplateOption SelectedTemplateOption
    {
        get => _selectedTemplateOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedTemplateOption, value))
            {
                OnPropertyChanged(nameof(SelectedTemplateKind));
            }
        }
    }

    public ReviewTemplateKind SelectedTemplateKind => _selectedTemplateOption.Kind;

    public string MetadataNotes
    {
        get => _metadataNotes;
        set
        {
            if (value is null)
            {
                value = string.Empty;
            }

            SetProperty(ref _metadataNotes, value);
        }
    }

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

    public RelayCommand<StageBlueprintViewModel> PreviewStageCommand { get; }

    public RelayCommand NextCommand { get; }

    public RelayCommand BackCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public IAsyncRelayCommand ChangeRunCommand { get; }

    public ProjectBlueprint? Result { get; private set; }

    public void Initialize(ProjectBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        Result = null;
        ApplyBlueprint(blueprint);
        CurrentStep = ProjectEditorStep.RunAndBasics;
        ErrorMessage = null;
        UpdateNavigationState();
    }

    public void ConfigureRunReloadHandler(Func<CancellationToken, Task<ProjectBlueprint?>> handler)
    {
        _runReloadHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        ChangeRunCommand.NotifyCanExecuteChanged();
    }

    private bool CanEdit() => _template is not null && !IsBusy;

    private bool CanSave() => _template is not null && !IsBusy;

    private bool CanMoveNext() => !IsBusy && GetNextStep(CurrentStep).HasValue;

    private bool CanMoveBack() => !IsBusy && GetPreviousStep(CurrentStep).HasValue;

    private bool CanChangeRun() => _runReloadHandler is not null && !IsBusy;

    private bool CanModifyStage(StageBlueprintViewModel? stage)
    {
        return CanEdit() && stage is not null && _stages.Contains(stage);
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

    private bool CanPreviewStage(StageBlueprintViewModel? stage)
    {
        return stage is not null;
    }

    private void PreviewStage(StageBlueprintViewModel? stage)
    {
        if (stage is null)
        {
            return;
        }

        StagePreviewRequested?.Invoke(this, new StagePreviewRequestedEventArgs(stage));
    }

    private void AddStage()
    {
        if (_template is null)
        {
            return;
        }

        var defaultType = ReviewStageType.TitleScreening;
        var stage = new StageBlueprint(
            $"stage-def-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}",
            $"Stage {_stages.Count + 1}",
            defaultType,
            primaryReviewers: 1,
            secondaryReviewers: 0,
            requiresConsensus: false,
            minimumAgreements: 0,
            escalateOnDisagreement: false,
            StageDisplayProfileFactory.CreateDefault(defaultType));

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

    private void MoveNext()
    {
        if (!ValidateStep(CurrentStep, showErrors: true))
        {
            return;
        }

        var next = GetNextStep(CurrentStep);
        if (next.HasValue)
        {
            CurrentStep = next.Value;
        }
    }

    private void MoveBack()
    {
        var previous = GetPreviousStep(CurrentStep);
        if (previous.HasValue)
        {
            CurrentStep = previous.Value;
        }
    }

    private async Task ChangeRunAsync()
    {
        if (_runReloadHandler is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var blueprint = await _runReloadHandler(CancellationToken.None).ConfigureAwait(true);
            if (blueprint is not null)
            {
                ApplyBlueprint(blueprint);
                CurrentStep = ProjectEditorStep.RunAndBasics;
            }
        }
        catch (OperationCanceledException)
        {
            // ignore cancellation
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Save()
    {
        if (_template is null)
        {
            return;
        }

        if (!ValidateStep(ProjectEditorStep.RunAndBasics, showErrors: true))
        {
            CurrentStep = ProjectEditorStep.RunAndBasics;
            return;
        }

        ErrorMessage = null;

        var trimmedName = ProjectName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ErrorMessage = "Provide a project name.";
            CurrentStep = ProjectEditorStep.RunAndBasics;
            return;
        }

        if (_stages.Count == 0)
        {
            ErrorMessage = "Add at least one workflow stage.";
            CurrentStep = ProjectEditorStep.Workflow;
            return;
        }

        var stageBlueprints = new List<StageBlueprint>(_stages.Count);
        foreach (var stage in _stages)
        {
            if (!stage.TryBuild(out var blueprint, out var message))
            {
                ErrorMessage = message;
                SelectedStage = stage;
                CurrentStep = ProjectEditorStep.Workflow;
                return;
            }

            stageBlueprints.Add(blueprint);
        }

        Result = _template.With(
            trimmedName,
            stageBlueprints,
            SelectedTemplateKind,
            MetadataNotes);

        RequestClose(true);
    }

    private void Cancel()
    {
        Result = null;
        RequestClose(false);
    }

    private void ApplyBlueprint(ProjectBlueprint blueprint)
    {
        _template = blueprint;

        ProjectName = blueprint.Name;
        LitSearchEntryId = blueprint.LitSearchEntryId;
        LitSearchRunId = blueprint.LitSearchRunId;
        CheckedEntryCount = blueprint.CheckedEntryIds.Count;
        HookRelativePath = blueprint.HookRelativePath;
        CheckedEntrySummary = BuildCheckedEntrySummary(blueprint.CheckedEntryIds);
        MetadataNotes = blueprint.MetadataNotes;

        var matchingTemplate = _templateOptions.FirstOrDefault(option => option.Kind == blueprint.Template)
            ?? _templateOptions[0];
        SelectedTemplateOption = matchingTemplate;

        _stages.Clear();
        foreach (var stage in blueprint.Stages)
        {
            var viewModel = new StageBlueprintViewModel(stage);
            _stages.Add(viewModel);
        }

        SelectedStage = _stages.FirstOrDefault();
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

    private bool ValidateStep(ProjectEditorStep step, bool showErrors)
    {
        if (_template is null)
        {
            if (showErrors)
            {
                ErrorMessage = "Select a LitSearch run to continue.";
            }

            return false;
        }

        if (step == ProjectEditorStep.RunAndBasics)
        {
            var trimmedName = ProjectName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                if (showErrors)
                {
                    ErrorMessage = "Provide a project name.";
                }

                return false;
            }
        }

        if (showErrors)
        {
            ErrorMessage = null;
        }

        return true;
    }

    private ProjectEditorStep? GetNextStep(ProjectEditorStep step)
    {
        var index = Array.FindIndex(_steps, descriptor => descriptor.Step == step);
        if (index < 0 || index >= _steps.Length - 1)
        {
            return null;
        }

        return _steps[index + 1].Step;
    }

    private ProjectEditorStep? GetPreviousStep(ProjectEditorStep step)
    {
        var index = Array.FindIndex(_steps, descriptor => descriptor.Step == step);
        if (index <= 0)
        {
            return null;
        }

        return _steps[index - 1].Step;
    }

    private void OnStagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RemoveStageCommand.NotifyCanExecuteChanged();
        MoveStageUpCommand.NotifyCanExecuteChanged();
        MoveStageDownCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void UpdateNavigationState()
    {
        AddStageCommand.NotifyCanExecuteChanged();
        RemoveStageCommand.NotifyCanExecuteChanged();
        MoveStageUpCommand.NotifyCanExecuteChanged();
        MoveStageDownCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ChangeRunCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }
}
