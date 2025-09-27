#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.Services.Review.Design;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

internal sealed class StageBlueprintViewModel : ObservableObject
{
    private string _name = string.Empty;
    private ReviewStageType _stageType;
    private int _primaryReviewers;
    private int _secondaryReviewers;
    private bool _requiresConsensus;
    private int _minimumAgreements;
    private bool _escalateOnDisagreement;

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
        set => SetProperty(ref _stageType, value);
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
            EscalateOnDisagreement);
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
}
