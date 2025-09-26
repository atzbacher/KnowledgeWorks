using System;

namespace LM.Review.Core.Models.Analytics;

public sealed record ReviewerLoadBreakdown
{
    public ReviewerLoadBreakdown(
        string reviewerId,
        int activeAssignments,
        int completedAssignments,
        double averageDecisionLatencyHours,
        double throughputPerDay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);

        ReviewerId = reviewerId;
        ActiveAssignments = activeAssignments;
        CompletedAssignments = completedAssignments;
        AverageDecisionLatencyHours = averageDecisionLatencyHours;
        ThroughputPerDay = throughputPerDay;
    }

    public string ReviewerId { get; }

    public int ActiveAssignments { get; }

    public int CompletedAssignments { get; }

    public double AverageDecisionLatencyHours { get; }

    public double ThroughputPerDay { get; }
}
