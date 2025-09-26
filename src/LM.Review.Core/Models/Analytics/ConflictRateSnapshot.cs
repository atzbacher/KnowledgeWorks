namespace LM.Review.Core.Models.Analytics;

public sealed record ConflictRateSnapshot
{
    public ConflictRateSnapshot(
        int totalStages,
        int conflictCount,
        int escalatedCount,
        int resolvedCount,
        int openConflicts,
        double conflictRate,
        double escalationRate,
        double resolutionRate)
    {
        TotalStages = totalStages;
        ConflictCount = conflictCount;
        EscalatedCount = escalatedCount;
        ResolvedCount = resolvedCount;
        OpenConflicts = openConflicts;
        ConflictRate = conflictRate;
        EscalationRate = escalationRate;
        ResolutionRate = resolutionRate;
    }

    public int TotalStages { get; }

    public int ConflictCount { get; }

    public int EscalatedCount { get; }

    public int ResolvedCount { get; }

    public int OpenConflicts { get; }

    public double ConflictRate { get; }

    public double EscalationRate { get; }

    public double ResolutionRate { get; }
}
