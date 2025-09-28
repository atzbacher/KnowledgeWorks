using System;
using System.Collections.Generic;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Analytics;
using LM.Review.Core.Services;
using Xunit;

namespace LM.Review.Core.Tests.Services;

public sealed class ReviewAnalyticsServiceTests
{
    [Fact]
    public void CreateSnapshot_ComputesAggregateMetrics()
    {
        var reference = new DateTimeOffset(2024, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var titleDefinition = CreateStageDefinition(
            "title-stage",
            "Title Screening",
            ReviewStageType.TitleScreening,
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.RequireAgreement(2, escalateOnDisagreement: true, arbitrationRole: null));

        var fullTextDefinition = CreateStageDefinition(
            "full-text-stage",
            "Full Text",
            ReviewStageType.FullTextReview,
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1)
            }),
            StageConsensusPolicy.Disabled());

        var project = ReviewProject.Create(
            "proj-analytics",
            "Systematic Review",
            reference.AddDays(-30),
            new[] { titleDefinition, fullTextDefinition });

        var stage1 = CreateStage(
            project.Id,
            "stage-1",
            titleDefinition,
            reference.AddDays(-9),
            reference.AddDays(-9).AddHours(3),
            ConflictState.None,
            new[]
            {
                CreateAssignment(
                    "assign-1",
                    "stage-1",
                    "alice",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    reference.AddDays(-9),
                    reference.AddDays(-9).AddHours(1)),
                CreateAssignment(
                    "assign-2",
                    "stage-1",
                    "bob",
                    ReviewerRole.Secondary,
                    ScreeningStatus.Included,
                    reference.AddDays(-9),
                    reference.AddDays(-9).AddHours(1.5))
            });

        var stage2 = CreateStage(
            project.Id,
            "stage-2",
            titleDefinition,
            reference.AddDays(-8),
            completedAt: null,
            ConflictState.Escalated,
            new[]
            {
                CreateAssignment(
                    "assign-3",
                    "stage-2",
                    "alice",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    reference.AddDays(-8),
                    reference.AddDays(-8).AddHours(1)),
                CreateAssignment(
                    "assign-4",
                    "stage-2",
                    "bob",
                    ReviewerRole.Secondary,
                    ScreeningStatus.Excluded,
                    reference.AddDays(-8),
                    reference.AddDays(-8).AddHours(2))
            });

        var consensus = ConsensusOutcome.Create(
            "stage-3",
            approved: false,
            resultingState: ConflictState.Resolved,
            resolvedAtUtc: reference.AddDays(-7).AddHours(5));

        var stage3 = CreateStage(
            project.Id,
            "stage-3",
            titleDefinition,
            reference.AddDays(-7),
            reference.AddDays(-7).AddHours(6),
            ConflictState.Resolved,
            new[]
            {
                CreateAssignment(
                    "assign-5",
                    "stage-3",
                    "alice",
                    ReviewerRole.Primary,
                    ScreeningStatus.Excluded,
                    reference.AddDays(-7),
                    reference.AddDays(-7).AddHours(2)),
                CreateAssignment(
                    "assign-6",
                    "stage-3",
                    "bob",
                    ReviewerRole.Secondary,
                    ScreeningStatus.Excluded,
                    reference.AddDays(-7),
                    reference.AddDays(-7).AddHours(2.5))
            },
            consensus);

        var stage4 = CreateStage(
            project.Id,
            "stage-4",
            fullTextDefinition,
            reference.AddDays(-5),
            reference.AddDays(-5).AddHours(2),
            ConflictState.None,
            new[]
            {
                CreateAssignment(
                    "assign-7",
                    "stage-4",
                    "carol",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    reference.AddDays(-5),
                    reference.AddDays(-5).AddHours(2))
            });

        var service = new ReviewAnalyticsService();
        var snapshot = service.CreateSnapshot(
            project,
            new[] { stage1, stage2, stage3, stage4 },
            new ReviewAnalyticsQueryOptions(reference));

        Assert.Equal(project.Id, snapshot.ProjectId);
        Assert.Equal(reference, snapshot.GeneratedAt);

        Assert.Collection(
            snapshot.StageProgress,
            titleSnapshot =>
            {
                Assert.Equal(titleDefinition.Id, titleSnapshot.StageDefinitionId);
                Assert.Equal(3, titleSnapshot.TotalInstances);
                Assert.Equal(2, titleSnapshot.CompletedInstances);
                Assert.Equal(ReviewStageType.TitleScreening, titleSnapshot.StageType);
                Assert.Equal(2d / 3d, titleSnapshot.CompletionRate, 5);
                Assert.Equal(1d, titleSnapshot.AverageReviewerCompletion, 5);
            },
            fullTextSnapshot =>
            {
                Assert.Equal(fullTextDefinition.Id, fullTextSnapshot.StageDefinitionId);
                Assert.Equal(1, fullTextSnapshot.TotalInstances);
                Assert.Equal(1, fullTextSnapshot.CompletedInstances);
                Assert.Equal(ReviewStageType.FullTextReview, fullTextSnapshot.StageType);
                Assert.Equal(1d, fullTextSnapshot.CompletionRate, 5);
                Assert.Equal(1d, fullTextSnapshot.AverageReviewerCompletion, 5);
            });

        Assert.Collection(
            snapshot.ReviewerLoads,
            alice =>
            {
                Assert.Equal("alice", alice.ReviewerId);
                Assert.Equal(0, alice.ActiveAssignments);
                Assert.Equal(3, alice.CompletedAssignments);
                Assert.Equal(1.33, alice.AverageDecisionLatencyHours, 2);
                Assert.Equal(0.33, alice.ThroughputPerDay, 2);
            },
            bob =>
            {
                Assert.Equal("bob", bob.ReviewerId);
                Assert.Equal(0, bob.ActiveAssignments);
                Assert.Equal(3, bob.CompletedAssignments);
                Assert.Equal(2d, bob.AverageDecisionLatencyHours, 2);
                Assert.Equal(0.33, bob.ThroughputPerDay, 2);
            },
            carol =>
            {
                Assert.Equal("carol", carol.ReviewerId);
                Assert.Equal(0, carol.ActiveAssignments);
                Assert.Equal(1, carol.CompletedAssignments);
                Assert.Equal(2d, carol.AverageDecisionLatencyHours, 2);
                Assert.Equal(0.2, carol.ThroughputPerDay, 2);
            });

        Assert.Equal(4, snapshot.ConflictRates.TotalStages);
        Assert.Equal(2, snapshot.ConflictRates.ConflictCount);
        Assert.Equal(1, snapshot.ConflictRates.EscalatedCount);
        Assert.Equal(1, snapshot.ConflictRates.ResolvedCount);
        Assert.Equal(1, snapshot.ConflictRates.OpenConflicts);
        Assert.Equal(0.5, snapshot.ConflictRates.ConflictRate, 5);
        Assert.Equal(0.25, snapshot.ConflictRates.EscalationRate, 5);
        Assert.Equal(0.5, snapshot.ConflictRates.ResolutionRate, 5);

        Assert.Equal(4, snapshot.PrismaFlow.RecordsIdentified);
        Assert.Equal(4, snapshot.PrismaFlow.RecordsScreened);
        Assert.Equal(2, snapshot.PrismaFlow.RecordsIncluded);
        Assert.Equal(1, snapshot.PrismaFlow.RecordsExcluded);
        Assert.Equal(1, snapshot.PrismaFlow.RecordsEscalated);
        Assert.Equal(0, snapshot.PrismaFlow.PendingDecisions);
    }

    [Fact]
    public void CreateSnapshot_WithTimelineFilter_UsesFilteredAssignments()
    {
        var reference = new DateTimeOffset(2024, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var titleDefinition = CreateStageDefinition(
            "title-stage",
            "Title Screening",
            ReviewStageType.TitleScreening,
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.Disabled());

        var project = ReviewProject.Create(
            "proj-window",
            "Windowed Review",
            reference.AddDays(-60),
            new[] { titleDefinition });

        var oldStage = CreateStage(
            project.Id,
            "stage-old",
            titleDefinition,
            reference.AddDays(-20),
            reference.AddDays(-20).AddHours(2),
            ConflictState.None,
            new[]
            {
                CreateAssignment(
                    "assign-old-1",
                    "stage-old",
                    "alice",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    reference.AddDays(-20),
                    reference.AddDays(-20).AddHours(1)),
                CreateAssignment(
                    "assign-old-2",
                    "stage-old",
                    "bob",
                    ReviewerRole.Secondary,
                    ScreeningStatus.Excluded,
                    reference.AddDays(-20),
                    reference.AddDays(-20).AddHours(1.5))
            });

        var recentStage = CreateStage(
            project.Id,
            "stage-recent",
            titleDefinition,
            reference.AddDays(-3),
            reference.AddDays(-3).AddHours(3),
            ConflictState.None,
            new[]
            {
                CreateAssignment(
                    "assign-new-1",
                    "stage-recent",
                    "alice",
                    ReviewerRole.Primary,
                    ScreeningStatus.Included,
                    reference.AddDays(-3),
                    reference.AddDays(-3).AddHours(1)),
                CreateAssignment(
                    "assign-new-2",
                    "stage-recent",
                    "bob",
                    ReviewerRole.Secondary,
                    ScreeningStatus.Excluded,
                    reference.AddDays(-3),
                    reference.AddDays(-3).AddHours(1.5))
            });

        var filter = ReviewAnalyticsQuery.LastDays(7, reference);
        var options = new ReviewAnalyticsQueryOptions(
            reference,
            filter,
            ReviewAnalyticsQuery.ActivatedWithin(filter),
            ReviewAnalyticsQuery.CompletedWithin(filter));

        var service = new ReviewAnalyticsService();
        var snapshot = service.CreateSnapshot(project, new[] { oldStage, recentStage }, options);

        Assert.Single(snapshot.StageProgress);
        var progress = Assert.Single(snapshot.StageProgress);
        Assert.Equal(1, progress.TotalInstances);
        Assert.Equal(1, progress.CompletedInstances);
        Assert.Equal(1d, progress.AverageReviewerCompletion, 5);

        Assert.Collection(
            snapshot.ReviewerLoads,
            alice =>
            {
                Assert.Equal("alice", alice.ReviewerId);
                Assert.Equal(0, alice.ActiveAssignments);
                Assert.Equal(1, alice.CompletedAssignments);
                Assert.Equal(1d, alice.AverageDecisionLatencyHours, 2);
                Assert.Equal(0.14, alice.ThroughputPerDay, 2);
            },
            bob =>
            {
                Assert.Equal("bob", bob.ReviewerId);
                Assert.Equal(0, bob.ActiveAssignments);
                Assert.Equal(1, bob.CompletedAssignments);
                Assert.Equal(1.5, bob.AverageDecisionLatencyHours, 2);
                Assert.Equal(0.14, bob.ThroughputPerDay, 2);
            });

        Assert.Equal(1, snapshot.ConflictRates.TotalStages);
        Assert.Equal(0, snapshot.ConflictRates.ConflictCount);
        Assert.Equal(1, snapshot.PrismaFlow.RecordsIdentified);
        Assert.Equal(1, snapshot.PrismaFlow.RecordsScreened);
        Assert.Equal(0, snapshot.PrismaFlow.RecordsIncluded);
        Assert.Equal(0, snapshot.PrismaFlow.RecordsExcluded);
        Assert.Equal(0, snapshot.PrismaFlow.RecordsEscalated);
        Assert.Equal(0, snapshot.PrismaFlow.PendingDecisions);
    }

    private static StageDefinition CreateStageDefinition(
        string id,
        string name,
        ReviewStageType stageType,
        ReviewerRequirement requirement,
        StageConsensusPolicy policy)
    {
        var displayProfile = StageDisplayProfile.Create(stageType switch
        {
            ReviewStageType.FullTextReview => new[]
            {
                StageContentArea.BibliographySummary,
                StageContentArea.FullTextViewer,
                StageContentArea.ReviewerDecisionPanel
            },
            ReviewStageType.DataExtraction => new[]
            {
                StageContentArea.BibliographySummary,
                StageContentArea.DataExtractionWorkspace,
                StageContentArea.NotesPane
            },
            _ => new[]
            {
                StageContentArea.BibliographySummary,
                StageContentArea.InclusionExclusionChecklist,
                StageContentArea.ReviewerDecisionPanel
            }
        });

        return StageDefinition.Create(id, name, stageType, requirement, policy, displayProfile);
    }

    private static ReviewStage CreateStage(
        string projectId,
        string stageId,
        StageDefinition definition,
        DateTimeOffset activatedAt,
        DateTimeOffset? completedAt,
        ConflictState conflictState,
        IEnumerable<ScreeningAssignment> assignments,
        ConsensusOutcome? consensus = null) =>
        ReviewStage.Create(stageId, projectId, definition, assignments, conflictState, activatedAt, completedAt, consensus);

    private static ScreeningAssignment CreateAssignment(
        string assignmentId,
        string stageId,
        string reviewerId,
        ReviewerRole role,
        ScreeningStatus status,
        DateTimeOffset assignedAt,
        DateTimeOffset? completedAt)
    {
        ReviewerDecision? decision = null;
        if (status is ScreeningStatus.Included or ScreeningStatus.Excluded)
        {
            decision = ReviewerDecision.Create(assignmentId, reviewerId, status, completedAt!.Value);
        }

        return ScreeningAssignment.Create(
            assignmentId,
            stageId,
            reviewerId,
            role,
            status,
            assignedAt,
            completedAt,
            decision);
    }
}
