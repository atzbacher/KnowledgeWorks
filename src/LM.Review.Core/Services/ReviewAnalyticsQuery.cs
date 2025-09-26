using System;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;

namespace LM.Review.Core.Services;

public static class ReviewAnalyticsQuery
{
    public static AnalyticsTimelineFilter LastDays(int days, DateTimeOffset referenceTime)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "The number of days must be greater than zero.");
        }

        return new AnalyticsTimelineFilter(referenceTime.AddDays(-days), referenceTime);
    }

    public static Func<ReviewStage, bool> ActivatedWithin(AnalyticsTimelineFilter filter)
    {
        return stage => filter.Overlaps(stage.ActivatedAt, stage.CompletedAt);
    }

    public static Func<ScreeningAssignment, bool> AssignedWithin(AnalyticsTimelineFilter filter)
    {
        return assignment => filter.Overlaps(assignment.AssignedAt, assignment.CompletedAt ?? filter.To);
    }

    public static Func<ScreeningAssignment, bool> CompletedWithin(AnalyticsTimelineFilter filter)
    {
        return assignment => assignment.CompletedAt is not null && filter.Contains(assignment.CompletedAt.Value);
    }
}
