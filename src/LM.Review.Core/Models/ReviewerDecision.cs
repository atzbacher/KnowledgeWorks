namespace LM.Review.Core.Models;

public sealed record ReviewerDecision
{
    private ReviewerDecision(
        string assignmentId,
        string reviewerId,
        ScreeningStatus decision,
        DateTimeOffset decidedAt,
        string? notes)
    {
        AssignmentId = assignmentId;
        ReviewerId = reviewerId;
        Decision = decision;
        DecidedAt = decidedAt;
        Notes = notes;
    }

    public string AssignmentId { get; }

    public string ReviewerId { get; }

    public ScreeningStatus Decision { get; }

    public DateTimeOffset DecidedAt { get; }

    public string? Notes { get; }

    public static ReviewerDecision Create(
        string assignmentId,
        string reviewerId,
        ScreeningStatus decision,
        DateTimeOffset decidedAtUtc,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        EnsureUtc(decidedAtUtc, nameof(decidedAtUtc));

        if (decision is not ScreeningStatus.Included and not ScreeningStatus.Excluded)
        {
            throw new ArgumentException($"Reviewer decisions must resolve to an inclusion or exclusion, not '{decision}'.", nameof(decision));
        }

        return new ReviewerDecision(assignmentId.Trim(), reviewerId.Trim(), decision, decidedAtUtc, notes?.Trim());
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
