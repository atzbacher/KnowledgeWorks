namespace LM.Review.Core.Models;

public sealed record ConsensusOutcome
{
    private ConsensusOutcome(
        string stageId,
        bool approved,
        ConflictState resultingState,
        DateTimeOffset resolvedAt,
        string? notes,
        string? resolvedBy)
    {
        StageId = stageId;
        Approved = approved;
        ResultingState = resultingState;
        ResolvedAt = resolvedAt;
        Notes = notes;
        ResolvedBy = resolvedBy;
    }

    public string StageId { get; }

    public bool Approved { get; }

    public ConflictState ResultingState { get; }

    public DateTimeOffset ResolvedAt { get; }

    public string? Notes { get; }

    public string? ResolvedBy { get; }

    public static ConsensusOutcome Create(
        string stageId,
        bool approved,
        ConflictState resultingState,
        DateTimeOffset resolvedAtUtc,
        string? notes = null,
        string? resolvedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        EnsureUtc(resolvedAtUtc, nameof(resolvedAtUtc));

        if (approved && resultingState is not ConflictState.None and not ConflictState.Resolved)
        {
            throw new ArgumentException("An approved consensus must result in a resolved or neutral conflict state.", nameof(resultingState));
        }

        if (!approved && resultingState is ConflictState.None)
        {
            throw new ArgumentException("A rejected consensus cannot result in a neutral conflict state.", nameof(resultingState));
        }

        return new ConsensusOutcome(stageId.Trim(), approved, resultingState, resolvedAtUtc, notes?.Trim(), resolvedBy?.Trim());
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be provided in UTC.", parameterName);
        }
    }
}
