namespace LM.Review.Core.Models;

public sealed record ScreeningAssignment
{
    private ScreeningAssignment(
        string id,
        string stageId,
        string reviewerId,
        ReviewerRole role,
        ScreeningStatus status,
        DateTimeOffset assignedAt,
        DateTimeOffset? completedAt,
        ReviewerDecision? decision)
    {
        Id = id;
        StageId = stageId;
        ReviewerId = reviewerId;
        Role = role;
        Status = status;
        AssignedAt = assignedAt;
        CompletedAt = completedAt;
        Decision = decision;
    }

    public string Id { get; }

    public string StageId { get; }

    public string ReviewerId { get; }

    public ReviewerRole Role { get; }

    public ScreeningStatus Status { get; }

    public DateTimeOffset AssignedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public ReviewerDecision? Decision { get; }

    public static ScreeningAssignment Create(
        string id,
        string stageId,
        string reviewerId,
        ReviewerRole role,
        ScreeningStatus status,
        DateTimeOffset assignedAtUtc,
        DateTimeOffset? completedAtUtc = null,
        ReviewerDecision? decision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        EnsureUtc(assignedAtUtc, nameof(assignedAtUtc));

        if (completedAtUtc.HasValue)
        {
            EnsureUtc(completedAtUtc.Value, nameof(completedAtUtc));
            if (completedAtUtc < assignedAtUtc)
            {
                throw new ArgumentException("Completion timestamp cannot precede assignment timestamp.", nameof(completedAtUtc));
            }
        }

        if (RequiresCompletion(status) && completedAtUtc is null)
        {
            throw new InvalidOperationException($"Status '{status}' requires a completion timestamp.");
        }

        if (!RequiresCompletion(status) && completedAtUtc is not null)
        {
            throw new InvalidOperationException($"Status '{status}' must not define a completion timestamp.");
        }

        if (decision is not null)
        {
            if (!IsDecisionStatus(decision.Decision))
            {
                throw new InvalidOperationException($"Decision status '{decision.Decision}' is not a terminal screening decision.");
            }

            if (!string.Equals(decision.AssignmentId, id.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Decision assignment identifier does not match the assignment.");
            }

            if (!string.Equals(decision.ReviewerId, reviewerId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Decision reviewer identifier does not match the assignment.");
            }

            if (!IsDecisionStatus(status))
            {
                throw new InvalidOperationException("Assignment status must reflect the recorded reviewer decision.");
            }
        }

        if (IsDecisionStatus(status) && decision is null)
        {
            throw new InvalidOperationException("A reviewer decision must accompany a completed assignment.");
        }

        return new ScreeningAssignment(
            id.Trim(),
            stageId.Trim(),
            reviewerId.Trim(),
            role,
            status,
            assignedAtUtc,
            completedAtUtc,
            decision);
    }

    private static bool RequiresCompletion(ScreeningStatus status) => status is ScreeningStatus.Included or ScreeningStatus.Excluded or ScreeningStatus.Escalated;

    private static bool IsDecisionStatus(ScreeningStatus status) => status is ScreeningStatus.Included or ScreeningStatus.Excluded;

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
