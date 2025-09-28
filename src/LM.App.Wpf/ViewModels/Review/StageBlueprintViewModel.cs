#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.Services.Review.Design;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

public sealed class StageBlueprintViewModel : ObservableObject
{
    private string _name = string.Empty;
    private ReviewStageType _stageType;
    private int _primaryReviewers;
    private int _secondaryReviewers;
    private bool _requiresConsensus;
    private int _minimumAgreements;
    private bool _escalateOnDisagreement;
    private readonly ObservableCollection<StageDisplayOptionViewModel> _displayOptions;

    public StageBlueprintViewModel(StageBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        StageId = blueprint.StageId;
        _name = blueprint.Name;
        _stageType = blueprint.StageType;
        _primaryReviewers = blueprint.PrimaryReviewers;
        _secondaryReviewers = blueprint.SecondaryReviewers;
        _requiresConsensus = blueprint.RequiresConsensus;
        _minimumAgreements = blueprint.MinimumAgreements;
        _escalateOnDisagreement = blueprint.EscalateOnDisagreement;
        _displayOptions = new ObservableCollection<StageDisplayOptionViewModel>();
        RefreshDisplayOptions(_stageType, blueprint.DisplayProfile);
    }

    public string StageId { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (value is null)
            {
                value = string.Empty;
            }

            SetProperty(ref _name, value);
        }
    }

    public ReviewStageType StageType
    {
        get => _stageType;
        set
        {
            if (SetProperty(ref _stageType, value))
            {
                RefreshDisplayOptions(value, null);
            }
        }
    }

    public int PrimaryReviewers
    {
        get => _primaryReviewers;
        set
        {
            if (SetProperty(ref _primaryReviewers, Math.Max(0, value)))
            {
                EnsureMinimumAgreementsInRange();
                OnPropertyChanged(nameof(TotalReviewers));
            }
        }
    }

    public int SecondaryReviewers
    {
        get => _secondaryReviewers;
        set
        {
            if (SetProperty(ref _secondaryReviewers, Math.Max(0, value)))
            {
                EnsureMinimumAgreementsInRange();
                OnPropertyChanged(nameof(TotalReviewers));
            }
        }
    }

    public bool RequiresConsensus
    {
        get => _requiresConsensus;
        set => SetProperty(ref _requiresConsensus, value);
    }

    public int MinimumAgreements
    {
        get => _minimumAgreements;
        set
        {
            var clamped = value;
            if (clamped < 0)
            {
                clamped = 0;
            }

            if (SetProperty(ref _minimumAgreements, clamped))
            {
                EnsureMinimumAgreementsInRange();
            }
        }
    }

    public bool EscalateOnDisagreement
    {
        get => _escalateOnDisagreement;
        set => SetProperty(ref _escalateOnDisagreement, value);
    }

    public int TotalReviewers => PrimaryReviewers + SecondaryReviewers;

    public ObservableCollection<StageDisplayOptionViewModel> DisplayOptions => _displayOptions;

    public bool TryBuild(out StageBlueprint stage, out string? errorMessage)
    {
        stage = default!;
        errorMessage = null;

        var trimmedName = Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            errorMessage = "Provide a stage name.";
            return false;
        }

        if (TotalReviewers <= 0)
        {
            errorMessage = $"Stage '{trimmedName}' must assign at least one reviewer.";
            return false;
        }

        var selectedAreas = _displayOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Area)
            .ToList();

        if (selectedAreas.Count == 0)
        {
            errorMessage = $"Stage '{trimmedName}' must display at least one workspace area.";
            return false;
        }

        var minimum = RequiresConsensus
            ? Math.Clamp(MinimumAgreements <= 0 ? TotalReviewers : MinimumAgreements, 1, TotalReviewers)
            : 0;

        stage = new StageBlueprint(
            StageId,
            trimmedName,
            StageType,
            PrimaryReviewers,
            SecondaryReviewers,
            RequiresConsensus,
            minimum,
            EscalateOnDisagreement,
            StageDisplayProfile.Create(selectedAreas));
        return true;
    }

    private void EnsureMinimumAgreementsInRange()
    {
        var total = TotalReviewers;
        if (total <= 0)
        {
            return;
        }

        if (_minimumAgreements > total)
        {
            _minimumAgreements = total;
            OnPropertyChanged(nameof(MinimumAgreements));
        }
    }

    private void RefreshDisplayOptions(ReviewStageType stageType, StageDisplayProfile? blueprintProfile)
    {
        var availableAreas = StageDisplayProfileFactory.GetAvailableAreas(stageType);
        var selectedAreas = blueprintProfile?.ContentAreas ?? availableAreas;

        _displayOptions.Clear();
        foreach (var area in availableAreas)
        {
            var option = new StageDisplayOptionViewModel(area, selectedAreas.Contains(area));
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(DisplayOptions));
            _displayOptions.Add(option);
        }
    }
}
