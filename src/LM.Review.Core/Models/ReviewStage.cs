using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models;

public sealed class ReviewStage
{
    private ReviewStage(
        string id,
        string projectId,
        StageDefinition definition,
        IReadOnlyList<ScreeningAssignment> assignments,
        ConflictState conflictState,
        DateTimeOffset activatedAt,
        DateTimeOffset? completedAt,
        ConsensusOutcome? consensus)
    {
        Id = id;
        ProjectId = projectId;
        Definition = definition;
        Assignments = assignments;
        ConflictState = conflictState;
        ActivatedAt = activatedAt;
        CompletedAt = completedAt;
        Consensus = consensus;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public StageDefinition Definition { get; }

    public IReadOnlyList<ScreeningAssignment> Assignments { get; }

    public ConflictState ConflictState { get; }

    public DateTimeOffset ActivatedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public ConsensusOutcome? Consensus { get; }

    public bool IsComplete => CompletedAt.HasValue;

    public static ReviewStage Create(
        string id,
        string projectId,
        StageDefinition definition,
        IEnumerable<ScreeningAssignment> assignments,
        ConflictState conflictState,
        DateTimeOffset activatedAtUtc,
        DateTimeOffset? completedAtUtc = null,
        ConsensusOutcome? consensus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(assignments);
        EnsureUtc(activatedAtUtc, nameof(activatedAtUtc));

        if (completedAtUtc.HasValue)
        {
            EnsureUtc(completedAtUtc.Value, nameof(completedAtUtc));
            if (completedAtUtc < activatedAtUtc)
            {
                throw new ArgumentException("Completion timestamp cannot be earlier than activation timestamp.", nameof(completedAtUtc));
            }
        }

        var trimmedId = id.Trim();
        var trimmedProjectId = projectId.Trim();

        var assignmentList = new List<ScreeningAssignment>();
        foreach (var assignment in assignments)
        {
            ArgumentNullException.ThrowIfNull(assignment);
            if (!string.Equals(assignment.StageId, trimmedId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Assignment stage identifier must match the review stage identifier.");
            }

            assignmentList.Add(assignment);
        }

        ValidateAssignmentCardinality(definition, assignmentList);

        if (consensus is not null && !string.Equals(consensus.StageId, trimmedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Consensus outcome must reference the stage it belongs to.");
        }

        if (definition.ConsensusPolicy.RequiresConsensus && consensus is null && conflictState == ConflictState.Resolved)
        {
            throw new InvalidOperationException("A resolved conflict state requires a consensus outcome.");
        }

        if (!definition.ConsensusPolicy.RequiresConsensus && consensus is not null)
        {
            throw new InvalidOperationException("Consensus outcome provided for a stage that does not require consensus.");
        }

        var readOnlyAssignments = new ReadOnlyCollection<ScreeningAssignment>(assignmentList);

        return new ReviewStage(trimmedId, trimmedProjectId, definition, readOnlyAssignments, conflictState, activatedAtUtc, completedAtUtc, consensus);
    }

    private static void ValidateAssignmentCardinality(StageDefinition definition, IReadOnlyCollection<ScreeningAssignment> assignments)
    {
        var allowedRoles = new HashSet<ReviewerRole>(definition.ReviewerRequirement.Requirements.Keys);

        foreach (var (role, requiredCount) in definition.ReviewerRequirement.Requirements)
        {
            var actualCount = assignments.Count(assignment => assignment.Role == role);
            if (actualCount != requiredCount)
            {
                throw new InvalidOperationException($"Stage '{definition.Name}' expected {requiredCount} {role} reviewer(s) but found {actualCount}.");
            }
        }

        var unexpectedAssignment = assignments.FirstOrDefault(assignment => !allowedRoles.Contains(assignment.Role));
        if (unexpectedAssignment is not null)
        {
            throw new InvalidOperationException($"Assignment for role '{unexpectedAssignment.Role}' is not permitted by the stage definition.");
        }
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
