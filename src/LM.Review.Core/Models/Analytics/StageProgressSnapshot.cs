using System;

namespace LM.Review.Core.Models.Analytics;

public sealed record StageProgressSnapshot
{
    public StageProgressSnapshot(
        string stageDefinitionId,
        string stageName,
        ReviewStageType stageType,
        int totalInstances,
        int completedInstances,
        double completionRate,
        double averageReviewerCompletion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageDefinitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);

        StageDefinitionId = stageDefinitionId;
        StageName = stageName;
        StageType = stageType;
        TotalInstances = totalInstances;
        CompletedInstances = completedInstances;
        CompletionRate = completionRate;
        AverageReviewerCompletion = averageReviewerCompletion;
    }

    public string StageDefinitionId { get; }

    public string StageName { get; }

    public ReviewStageType StageType { get; }

    public int TotalInstances { get; }

    public int CompletedInstances { get; }

    public double CompletionRate { get; }

    public double AverageReviewerCompletion { get; }
}
