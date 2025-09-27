#nullable enable
using System;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review.Design;

internal sealed class StageBlueprint
{
    public StageBlueprint(
        string stageId,
        string name,
        ReviewStageType stageType,
        int primaryReviewers,
        int secondaryReviewers,
        bool requiresConsensus,
        int minimumAgreements,
        bool escalateOnDisagreement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (primaryReviewers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryReviewers), primaryReviewers, "Primary reviewer count cannot be negative.");
        }

        if (secondaryReviewers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(secondaryReviewers), secondaryReviewers, "Secondary reviewer count cannot be negative.");
        }

        StageId = stageId.Trim();
        Name = name.Trim();
        StageType = stageType;
        PrimaryReviewers = primaryReviewers;
        SecondaryReviewers = secondaryReviewers;
        RequiresConsensus = requiresConsensus;
        MinimumAgreements = minimumAgreements;
        EscalateOnDisagreement = escalateOnDisagreement;
    }

    public string StageId { get; }

    public string Name { get; }

    public ReviewStageType StageType { get; }

    public int PrimaryReviewers { get; }

    public int SecondaryReviewers { get; }

    public bool RequiresConsensus { get; }

    public int MinimumAgreements { get; }

    public bool EscalateOnDisagreement { get; }
}
