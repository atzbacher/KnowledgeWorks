using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LM.Review.Core.Models.Analytics;

public sealed class ProjectAnalyticsSnapshot
{
    public ProjectAnalyticsSnapshot(
        string projectId,
        DateTimeOffset generatedAt,
        IEnumerable<StageProgressSnapshot> stageProgress,
        IEnumerable<ReviewerLoadBreakdown> reviewerLoads,
        ConflictRateSnapshot conflictRates,
        PrismaFlowSnapshot prismaFlow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(stageProgress);
        ArgumentNullException.ThrowIfNull(reviewerLoads);
        ArgumentNullException.ThrowIfNull(conflictRates);
        ArgumentNullException.ThrowIfNull(prismaFlow);

        ProjectId = projectId;
        GeneratedAt = generatedAt;
        StageProgress = new ReadOnlyCollection<StageProgressSnapshot>(stageProgress.ToList());
        ReviewerLoads = new ReadOnlyCollection<ReviewerLoadBreakdown>(reviewerLoads.ToList());
        ConflictRates = conflictRates;
        PrismaFlow = prismaFlow;
    }

    public string ProjectId { get; }

    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyList<StageProgressSnapshot> StageProgress { get; }

    public IReadOnlyList<ReviewerLoadBreakdown> ReviewerLoads { get; }

    public ConflictRateSnapshot ConflictRates { get; }

    public PrismaFlowSnapshot PrismaFlow { get; }
}
