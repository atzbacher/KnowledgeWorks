using System;

namespace LM.Review.Core.Models.Analytics;

public readonly struct AnalyticsTimelineFilter
{
    public AnalyticsTimelineFilter(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
        {
            throw new ArgumentException("The end of a timeline filter must be greater than or equal to the start.", nameof(to));
        }

        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    public TimeSpan Duration => To - From;

    public bool Contains(DateTimeOffset timestamp) => timestamp >= From && timestamp <= To;

    public bool Overlaps(DateTimeOffset start, DateTimeOffset? end = null)
    {
        var effectiveEnd = end ?? To;
        return start <= To && effectiveEnd >= From;
    }
}
