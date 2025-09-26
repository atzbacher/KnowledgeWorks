using System;
using System.Collections.Generic;
using System.Linq;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;

namespace LM.Review.Core.Services;

public sealed class ReviewAnalyticsService : IReviewAnalyticsService
{
    public ProjectAnalyticsSnapshot CreateSnapshot(
        ReviewProject project,
        IEnumerable<ReviewStage> stages,
        ReviewAnalyticsQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stages);

        var resolvedOptions = options ?? ReviewAnalyticsQueryOptions.CreateDefault(DateTimeOffset.UtcNow);
        var stageList = new List<ReviewStage>();

        foreach (var stage in stages)
        {
            ArgumentNullException.ThrowIfNull(stage);
            if (!string.Equals(stage.ProjectId, project.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (resolvedOptions.StageFilter is not null && !resolvedOptions.StageFilter(stage))
            {
                continue;
            }

            stageList.Add(stage);
        }

        var assignmentsByStage = new Dictionary<string, IReadOnlyList<ScreeningAssignment>>(StringComparer.Ordinal);
        var allAssignments = new List<ScreeningAssignment>();

        foreach (var stage in stageList)
        {
            var filteredAssignments = new List<ScreeningAssignment>();
            foreach (var assignment in stage.Assignments)
            {
                if (resolvedOptions.AssignmentFilter is not null && !resolvedOptions.AssignmentFilter(assignment))
                {
                    continue;
                }

                filteredAssignments.Add(assignment);
                allAssignments.Add(assignment);
            }

            assignmentsByStage[stage.Id] = filteredAssignments;
        }

        var stageProgress = BuildStageProgress(project.StageDefinitions, stageList, assignmentsByStage);
        var reviewerLoads = BuildReviewerLoads(allAssignments, resolvedOptions.ReferenceTime, resolvedOptions.TimelineFilter);
        var conflictRates = BuildConflictRates(stageList);
        var prismaFlow = BuildPrismaFlow(stageList, assignmentsByStage);

        return new ProjectAnalyticsSnapshot(
            project.Id,
            resolvedOptions.ReferenceTime,
            stageProgress,
            reviewerLoads,
            conflictRates,
            prismaFlow);
    }

    private static IReadOnlyList<StageProgressSnapshot> BuildStageProgress(
        IReadOnlyList<StageDefinition> stageDefinitions,
        IReadOnlyList<ReviewStage> stages,
        IReadOnlyDictionary<string, IReadOnlyList<ScreeningAssignment>> assignmentsByStage)
    {
        var snapshots = new List<StageProgressSnapshot>(stageDefinitions.Count);

        foreach (var definition in stageDefinitions)
        {
            var matchingStages = stages
                .Where(stage => string.Equals(stage.Definition.Id, definition.Id, StringComparison.Ordinal))
                .ToList();

            var totalInstances = matchingStages.Count;
            var completedInstances = matchingStages.Count(stage => stage.IsComplete);
            var reviewerRequirement = Math.Max(1, definition.ReviewerRequirement.TotalRequired);
            var reviewerCompletionAccumulator = 0d;

            foreach (var stage in matchingStages)
            {
                var stageAssignments = assignmentsByStage.TryGetValue(stage.Id, out var list)
                    ? list
                    : Array.Empty<ScreeningAssignment>();

                var completedCount = stageAssignments.Count(assignment => IsTerminalStatus(assignment.Status));
                reviewerCompletionAccumulator += Math.Clamp((double)completedCount / reviewerRequirement, 0d, 1d);
            }

            var completionRate = totalInstances == 0 ? 0d : (double)completedInstances / totalInstances;
            var averageReviewerCompletion = totalInstances == 0 ? 0d : reviewerCompletionAccumulator / totalInstances;

            snapshots.Add(new StageProgressSnapshot(
                definition.Id,
                definition.Name,
                definition.StageType,
                totalInstances,
                completedInstances,
                completionRate,
                averageReviewerCompletion));
        }

        return snapshots;
    }

    private static IReadOnlyList<ReviewerLoadBreakdown> BuildReviewerLoads(
        IReadOnlyList<ScreeningAssignment> assignments,
        DateTimeOffset referenceTime,
        AnalyticsTimelineFilter? timelineFilter)
    {
        if (assignments.Count == 0)
        {
            return Array.Empty<ReviewerLoadBreakdown>();
        }

        var groups = assignments.GroupBy(assignment => assignment.ReviewerId, StringComparer.Ordinal);
        var results = new List<ReviewerLoadBreakdown>();

        foreach (var group in groups)
        {
            var reviewerAssignments = group.ToList();
            var active = reviewerAssignments.Count(assignment => IsActiveStatus(assignment.Status));
            var completed = reviewerAssignments.Count(assignment => IsTerminalStatus(assignment.Status));

            var latencyValues = reviewerAssignments
                .Where(assignment => assignment.CompletedAt.HasValue)
                .Select(assignment => (assignment.CompletedAt!.Value - assignment.AssignedAt).TotalHours)
                .ToList();

            var averageLatency = latencyValues.Count == 0 ? 0d : latencyValues.Average();

            double measurementWindowDays;
            if (timelineFilter.HasValue)
            {
                measurementWindowDays = Math.Max(1d, timelineFilter.Value.Duration.TotalDays);
            }
            else
            {
                var earliest = reviewerAssignments.Min(assignment => assignment.AssignedAt);
                var span = (referenceTime - earliest).TotalDays;
                measurementWindowDays = Math.Max(1d, span);
            }

            var throughput = completed / measurementWindowDays;

            results.Add(new ReviewerLoadBreakdown(
                group.Key,
                active,
                completed,
                Math.Round(averageLatency, 2, MidpointRounding.AwayFromZero),
                Math.Round(throughput, 2, MidpointRounding.AwayFromZero)));
        }

        return results
            .OrderByDescending(result => result.ActiveAssignments)
            .ThenBy(result => result.ReviewerId, StringComparer.Ordinal)
            .ToList();
    }

    private static ConflictRateSnapshot BuildConflictRates(IReadOnlyList<ReviewStage> stages)
    {
        if (stages.Count == 0)
        {
            return new ConflictRateSnapshot(0, 0, 0, 0, 0, 0d, 0d, 0d);
        }

        var totalStages = stages.Count;
        var conflictCount = stages.Count(stage => stage.ConflictState != ConflictState.None);
        var escalatedCount = stages.Count(stage => stage.ConflictState == ConflictState.Escalated);
        var resolvedCount = stages.Count(stage => stage.ConflictState == ConflictState.Resolved);
        var openConflicts = stages.Count(stage => stage.ConflictState is ConflictState.Conflict or ConflictState.Escalated);

        var conflictRate = (double)conflictCount / totalStages;
        var escalationRate = (double)escalatedCount / totalStages;
        var resolutionRate = conflictCount == 0 ? 0d : (double)resolvedCount / conflictCount;

        return new ConflictRateSnapshot(
            totalStages,
            conflictCount,
            escalatedCount,
            resolvedCount,
            openConflicts,
            conflictRate,
            escalationRate,
            resolutionRate);
    }

    private static PrismaFlowSnapshot BuildPrismaFlow(
        IReadOnlyList<ReviewStage> stages,
        IReadOnlyDictionary<string, IReadOnlyList<ScreeningAssignment>> assignmentsByStage)
    {
        var prismaStages = stages
            .Where(stage => stage.Definition.StageType is ReviewStageType.TitleScreening or ReviewStageType.FullTextReview)
            .ToList();

        if (prismaStages.Count == 0)
        {
            return new PrismaFlowSnapshot(0, 0, 0, 0, 0, 0);
        }

        var identified = prismaStages.Count;
        var screened = 0;
        var included = 0;
        var excluded = 0;
        var escalated = 0;

        foreach (var stage in prismaStages)
        {
            if (stage.ConflictState == ConflictState.Escalated)
            {
                escalated++;
            }

            var assignments = assignmentsByStage.TryGetValue(stage.Id, out var list)
                ? list
                : Array.Empty<ScreeningAssignment>();

            var allTerminal = assignments.Count > 0 && assignments.All(assignment => IsTerminalStatus(assignment.Status));

            if (stage.IsComplete || allTerminal)
            {
                screened++;
            }

            var consensus = stage.Consensus;
            if (consensus is not null)
            {
                if (consensus.Approved)
                {
                    included++;
                }
                else
                {
                    excluded++;
                }

                continue;
            }

            if (!allTerminal)
            {
                continue;
            }

            if (assignments.All(assignment => assignment.Status == ScreeningStatus.Included))
            {
                included++;
            }
            else if (assignments.All(assignment => assignment.Status == ScreeningStatus.Excluded))
            {
                excluded++;
            }
        }

        var pending = Math.Max(0, identified - screened);

        return new PrismaFlowSnapshot(identified, screened, included, excluded, escalated, pending);
    }

    private static bool IsTerminalStatus(ScreeningStatus status) =>
        status is ScreeningStatus.Included or ScreeningStatus.Excluded;

    private static bool IsActiveStatus(ScreeningStatus status) =>
        status is ScreeningStatus.Pending or ScreeningStatus.InProgress or ScreeningStatus.Escalated;
}
