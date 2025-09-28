using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace LM.Review.Core.Models;

public sealed record StageDefinition
{
    private StageDefinition(
        string id,
        string name,
        ReviewStageType stageType,
        ReviewerRequirement reviewerRequirement,
        StageConsensusPolicy consensusPolicy,
        StageDisplayProfile displayProfile)
    {
        Id = id;
        Name = name;
        StageType = stageType;
        ReviewerRequirement = reviewerRequirement;
        ConsensusPolicy = consensusPolicy;
        DisplayProfile = displayProfile;
    }

    public string Id { get; }

    public string Name { get; }

    public ReviewStageType StageType { get; }

    public ReviewerRequirement ReviewerRequirement { get; }

    public StageConsensusPolicy ConsensusPolicy { get; }

    public StageDisplayProfile DisplayProfile { get; }

    public static StageDefinition Create(
        string id,
        string name,
        ReviewStageType stageType,
        ReviewerRequirement reviewerRequirement,
        StageConsensusPolicy consensusPolicy,
        StageDisplayProfile displayProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(reviewerRequirement);
        ArgumentNullException.ThrowIfNull(consensusPolicy);
        ArgumentNullException.ThrowIfNull(displayProfile);

        var trimmedId = id.Trim();
        var trimmedName = name.Trim();

        reviewerRequirement.EnsureHasReviewers();
        consensusPolicy.EnsureCompatibility(reviewerRequirement);

        return new StageDefinition(trimmedId, trimmedName, stageType, reviewerRequirement, consensusPolicy, displayProfile);
    }
}

public sealed record ReviewerRequirement
{
    private readonly ReadOnlyDictionary<ReviewerRole, int> _requirements;

    private ReviewerRequirement(ReadOnlyDictionary<ReviewerRole, int> requirements)
    {
        _requirements = requirements;
    }

    public IReadOnlyDictionary<ReviewerRole, int> Requirements => _requirements;

    public int TotalRequired => _requirements.Values.Sum();

    public int GetRequirement(ReviewerRole role) => _requirements.TryGetValue(role, out var count) ? count : 0;

    public static ReviewerRequirement Create(IEnumerable<KeyValuePair<ReviewerRole, int>> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var requirementDictionary = new Dictionary<ReviewerRole, int>();
        foreach (var requirement in requirements)
        {
            if (requirement.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requirements),
                    string.Format(CultureInfo.InvariantCulture, "Reviewer requirement for role {0} must be greater than zero.", requirement.Key));
            }

            if (requirementDictionary.ContainsKey(requirement.Key))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Duplicate reviewer requirement defined for role {0}.", requirement.Key),
                    nameof(requirements));
            }

            requirementDictionary[requirement.Key] = requirement.Value;
        }

        return new ReviewerRequirement(new ReadOnlyDictionary<ReviewerRole, int>(requirementDictionary));
    }

    internal void EnsureHasReviewers()
    {
        if (_requirements.Count == 0)
        {
            throw new InvalidOperationException("At least one reviewer requirement must be defined for a stage.");
        }
    }
}

public sealed record StageConsensusPolicy
{
    private StageConsensusPolicy(bool requiresConsensus, int minimumAgreements, bool escalateOnDisagreement, ReviewerRole? arbitrationRole)
    {
        RequiresConsensus = requiresConsensus;
        MinimumAgreements = minimumAgreements;
        EscalateOnDisagreement = escalateOnDisagreement;
        ArbitrationRole = arbitrationRole;
    }

    public bool RequiresConsensus { get; }

    public int MinimumAgreements { get; }

    public bool EscalateOnDisagreement { get; }

    public ReviewerRole? ArbitrationRole { get; }

    public static StageConsensusPolicy Disabled() => new(false, 0, false, null);

    public static StageConsensusPolicy RequireAgreement(int minimumAgreements, bool escalateOnDisagreement, ReviewerRole? arbitrationRole)
    {
        if (minimumAgreements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAgreements), "Minimum agreements must be greater than zero when consensus is required.");
        }

        return new StageConsensusPolicy(true, minimumAgreements, escalateOnDisagreement, arbitrationRole);
    }

    internal void EnsureCompatibility(ReviewerRequirement requirement)
    {
        if (!RequiresConsensus)
        {
            return;
        }

        if (MinimumAgreements > requirement.TotalRequired)
        {
            throw new InvalidOperationException("Minimum agreements cannot exceed the total number of required reviewers for the stage.");
        }

        if (ArbitrationRole.HasValue && requirement.GetRequirement(ArbitrationRole.Value) == 0)
        {
            throw new InvalidOperationException("An arbitration role was provided but is not part of the reviewer requirements.");
        }
    }
}
