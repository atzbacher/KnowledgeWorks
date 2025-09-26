using System;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;

namespace LM.Review.Core.Services;

public sealed record ReviewAnalyticsQueryOptions
{
    public ReviewAnalyticsQueryOptions(
        DateTimeOffset referenceTime,
        AnalyticsTimelineFilter? timelineFilter = null,
        Func<ReviewStage, bool>? stageFilter = null,
        Func<ScreeningAssignment, bool>? assignmentFilter = null)
    {
        ReferenceTime = referenceTime;
        TimelineFilter = timelineFilter;
        StageFilter = stageFilter;
        AssignmentFilter = assignmentFilter;
    }

    public DateTimeOffset ReferenceTime { get; }

    public AnalyticsTimelineFilter? TimelineFilter { get; }

    public Func<ReviewStage, bool>? StageFilter { get; }

    public Func<ScreeningAssignment, bool>? AssignmentFilter { get; }

    public static ReviewAnalyticsQueryOptions CreateDefault(DateTimeOffset referenceTime) => new(referenceTime);
}
