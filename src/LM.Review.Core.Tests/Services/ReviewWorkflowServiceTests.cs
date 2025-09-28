using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;
using Xunit;

namespace LM.Review.Core.Tests.Services;

public sealed class ReviewWorkflowServiceTests
{
    [Fact]
    public async Task CreateStageAsync_CreatesAssignmentsWithinQuota()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2024, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new FakeReviewWorkflowStore();
        var hookFactory = new FakeHookContextFactory();
        var orchestrator = new FakeHookOrchestrator();
        var service = new ReviewWorkflowService(store, orchestrator, hookFactory, timeProvider);

        var definition = CreateStageDefinition(
            "screen-1",
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.Disabled());

        var project = ReviewProject.Create(
            "entry-1",
            "Literature Review",
            timeProvider.GetUtcNow(),
            new[] { definition });

        store.AddProject(project);

        var stage = await service.CreateStageAsync(
            project.Id,
            definition.Id,
            new[]
            {
                new ReviewerAssignmentRequest("alice", ReviewerRole.Primary),
                new ReviewerAssignmentRequest("bob", ReviewerRole.Secondary)
            });

        Assert.Equal(project.Id, stage.ProjectId);
        Assert.Equal(2, stage.Assignments.Count);
        Assert.All(stage.Assignments, assignment => Assert.Equal(ScreeningStatus.Pending, assignment.Status));

        var persistedStage = await store.GetStageAsync(stage.Id, CancellationToken.None);
        Assert.NotNull(persistedStage);
        var persistedAssignments = await store.GetAssignmentsByStageAsync(stage.Id, CancellationToken.None);
        Assert.Equal(2, persistedAssignments.Count);

        Assert.Equal(2, orchestrator.Calls.Count);
        Assert.All(orchestrator.Calls, call => Assert.Equal(project.Id, call.EntryId));
        Assert.Equal(2, hookFactory.Contexts.Count(ctx => ctx.Action == "assignment.updated"));
    }

    [Fact]
    public async Task CreateStageAsync_WhenAssignmentsDoNotMatchQuota_Throws()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2024, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new FakeReviewWorkflowStore();
        var hookFactory = new FakeHookContextFactory();
        var orchestrator = new FakeHookOrchestrator();
        var service = new ReviewWorkflowService(store, orchestrator, hookFactory, timeProvider);

        var definition = CreateStageDefinition(
            "screen-1",
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.Disabled());

        var project = ReviewProject.Create(
            "entry-2",
            "Mismatch",
            timeProvider.GetUtcNow(),
            new[] { definition });

        store.AddProject(project);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateStageAsync(
            project.Id,
            definition.Id,
            new[] { new ReviewerAssignmentRequest("alice", ReviewerRole.Primary) }));

        Assert.Empty(store.StageIds);
    }

    [Fact]
    public async Task SubmitDecisionAsync_AllReviewersAgree_CompletesStage()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2024, 10, 1, 8, 0, 0, TimeSpan.Zero));
        var store = new FakeReviewWorkflowStore();
        var hookFactory = new FakeHookContextFactory();
        var orchestrator = new FakeHookOrchestrator();
        var service = new ReviewWorkflowService(store, orchestrator, hookFactory, timeProvider);

        var definition = CreateStageDefinition(
            "screen-1",
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.Disabled());

        var project = ReviewProject.Create(
            "entry-3",
            "Consensus",
            timeProvider.GetUtcNow(),
            new[] { definition });
        store.AddProject(project);

        var activatedAt = timeProvider.GetUtcNow();
        var primaryDecision = ReviewerDecision.Create(
            "assign-1",
            "alice",
            ScreeningStatus.Included,
            activatedAt.AddMinutes(10));

        var primaryAssignment = ScreeningAssignment.Create(
            primaryDecision.AssignmentId,
            "stage-1",
            primaryDecision.ReviewerId,
            ReviewerRole.Primary,
            ScreeningStatus.Included,
            activatedAt,
            primaryDecision.DecidedAt,
            primaryDecision);

        var secondaryAssignment = ScreeningAssignment.Create(
            "assign-2",
            "stage-1",
            "bob",
            ReviewerRole.Secondary,
            ScreeningStatus.Pending,
            activatedAt);

        var stage = ReviewStage.Create(
            "stage-1",
            project.Id,
            definition,
            new[] { primaryAssignment, secondaryAssignment },
            ConflictState.None,
            activatedAt);

        await store.SaveStageAsync(stage, CancellationToken.None);
        await store.SaveAssignmentAsync(project.Id, primaryAssignment, CancellationToken.None);
        await store.SaveAssignmentAsync(project.Id, secondaryAssignment, CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(30));

        var updated = await service.SubmitDecisionAsync(
            secondaryAssignment.Id,
            ScreeningStatus.Included,
            "matches",
            CancellationToken.None);

        Assert.Equal(ScreeningStatus.Included, updated.Status);

        var storedStage = await store.GetStageAsync(stage.Id, CancellationToken.None);
        Assert.NotNull(storedStage);
        Assert.Equal(ConflictState.Resolved, storedStage!.ConflictState);
        Assert.Equal(timeProvider.GetUtcNow(), storedStage.CompletedAt);

        var storedAssignments = await store.GetAssignmentsByStageAsync(stage.Id, CancellationToken.None);
        Assert.All(storedAssignments, assignment => Assert.Equal(ScreeningStatus.Included, assignment.Status));

        Assert.Equal(3, orchestrator.Calls.Count);
        Assert.Contains(hookFactory.Contexts, ctx => ctx.Action == "stage.transition" && ctx.Tags["to"] == ConflictState.Resolved.ToString());
    }

    [Fact]
    public async Task SubmitDecisionAsync_DivergentDecisions_EscalatesAndCreatesConsensusStage()
    {
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2024, 10, 1, 7, 30, 0, TimeSpan.Zero));
        var store = new FakeReviewWorkflowStore();
        var hookFactory = new FakeHookContextFactory();
        var orchestrator = new FakeHookOrchestrator();
        var service = new ReviewWorkflowService(store, orchestrator, hookFactory, timeProvider);

        var screeningDefinition = CreateStageDefinition(
            "screen-1",
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.RequireAgreement(minimumAgreements: 2, escalateOnDisagreement: true, arbitrationRole: null));

        var consensusDefinition = CreateStageDefinition(
            "consensus-1",
            ReviewerRequirement.Create(new[]
            {
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
            }),
            StageConsensusPolicy.Disabled(),
            ReviewStageType.ConsensusMeeting);

        var project = ReviewProject.Create(
            "entry-4",
            "Escalation",
            timeProvider.GetUtcNow(),
            new[] { screeningDefinition, consensusDefinition });
        store.AddProject(project);

        var activatedAt = timeProvider.GetUtcNow();
        var primaryDecision = ReviewerDecision.Create(
            "assign-1",
            "alice",
            ScreeningStatus.Included,
            activatedAt.AddMinutes(5));

        var primaryAssignment = ScreeningAssignment.Create(
            primaryDecision.AssignmentId,
            "stage-1",
            primaryDecision.ReviewerId,
            ReviewerRole.Primary,
            ScreeningStatus.Included,
            activatedAt,
            primaryDecision.DecidedAt,
            primaryDecision);

        var secondaryAssignment = ScreeningAssignment.Create(
            "assign-2",
            "stage-1",
            "bob",
            ReviewerRole.Secondary,
            ScreeningStatus.Pending,
            activatedAt);

        var stage = ReviewStage.Create(
            "stage-1",
            project.Id,
            screeningDefinition,
            new[] { primaryAssignment, secondaryAssignment },
            ConflictState.None,
            activatedAt);

        await store.SaveStageAsync(stage, CancellationToken.None);
        await store.SaveAssignmentAsync(project.Id, primaryAssignment, CancellationToken.None);
        await store.SaveAssignmentAsync(project.Id, secondaryAssignment, CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(20));

        var updatedAssignment = await service.SubmitDecisionAsync(
            secondaryAssignment.Id,
            ScreeningStatus.Excluded,
            "disagree",
            CancellationToken.None);

        Assert.Equal(ScreeningStatus.Excluded, updatedAssignment.Status);

        var updatedStage = await store.GetStageAsync(stage.Id, CancellationToken.None);
        Assert.NotNull(updatedStage);
        Assert.Equal(ConflictState.Escalated, updatedStage!.ConflictState);

        var stages = await store.GetStagesByProjectAsync(project.Id, CancellationToken.None);
        var consensusStage = stages.FirstOrDefault(s => s.Definition.StageType == ReviewStageType.ConsensusMeeting && s.Id != stage.Id);
        Assert.NotNull(consensusStage);
        Assert.All(consensusStage!.Assignments, assignment => Assert.Equal(ScreeningStatus.Pending, assignment.Status));
        Assert.Equal(2, consensusStage.Assignments.Count);

        Assert.True(orchestrator.Calls.Count >= 4);
        Assert.Contains(hookFactory.Contexts, ctx => ctx.Action == "stage.transition" && ctx.Tags["to"] == ConflictState.Escalated.ToString());
    }

    private static StageDefinition CreateStageDefinition(
        string id,
        ReviewerRequirement requirement,
        StageConsensusPolicy consensus,
        ReviewStageType stageType = ReviewStageType.TitleScreening)
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

        return StageDefinition.Create(id, id, stageType, requirement, consensus, displayProfile);
    }

    private sealed class FakeReviewWorkflowStore : IReviewWorkflowStore
    {
        private readonly Dictionary<string, ReviewProject> _projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ReviewStage> _stages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ScreeningAssignment>> _assignmentsByStage = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScreeningAssignment> _assignments = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> StageIds => _stages.Keys.ToList();

        public void AddProject(ReviewProject project)
        {
            _projects[project.Id] = project;
        }

        public Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            _projects.TryGetValue(projectId, out var project);
            return Task.FromResult(project);
        }

        public Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewProject> snapshot = _projects.Values.ToList();
            return Task.FromResult(snapshot);
        }

        public Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken)
        {
            _stages.TryGetValue(stageId, out var stage);
            return Task.FromResult(stage);
        }

        public Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            var stages = _stages.Values.Where(stage => string.Equals(stage.ProjectId, projectId, StringComparison.Ordinal)).ToList();
            return Task.FromResult<IReadOnlyList<ReviewStage>>(stages);
        }

        public Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
        {
            _assignments.TryGetValue(assignmentId, out var assignment);
            return Task.FromResult(assignment);
        }

        public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
        {
            if (_assignmentsByStage.TryGetValue(stageId, out var assignments))
            {
                return Task.FromResult<IReadOnlyList<ScreeningAssignment>>(assignments.ToList());
            }

            return Task.FromResult<IReadOnlyList<ScreeningAssignment>>(Array.Empty<ScreeningAssignment>());
        }

        public Task SaveProjectAsync(ReviewProject project, CancellationToken cancellationToken)
        {
            _projects[project.Id] = project;
            return Task.CompletedTask;
        }

        public Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
        {
            _stages[stage.Id] = stage;
            _assignmentsByStage[stage.Id] = stage.Assignments.ToList();
            foreach (var assignment in stage.Assignments)
            {
                _assignments[assignment.Id] = assignment;
            }

            return Task.CompletedTask;
        }

        public Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
        {
            _assignments[assignment.Id] = assignment;
            if (!_assignmentsByStage.TryGetValue(assignment.StageId, out var assignments))
            {
                assignments = new List<ScreeningAssignment>();
                _assignmentsByStage[assignment.StageId] = assignments;
            }

            var index = assignments.FindIndex(existing => existing.Id == assignment.Id);
            if (index >= 0)
            {
                assignments[index] = assignment;
            }
            else
            {
                assignments.Add(assignment);
            }

            if (_stages.TryGetValue(assignment.StageId, out var stage))
            {
                var updated = ReviewStage.Create(
                    stage.Id,
                    stage.ProjectId,
                    stage.Definition,
                    assignments,
                    stage.ConflictState,
                    stage.ActivatedAt,
                    stage.CompletedAt,
                    stage.Consensus);
                _stages[stage.Id] = updated;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeHookContextFactory : IReviewHookContextFactory
    {
        public sealed record TestHookContext(string Action, Dictionary<string, string> Tags) : IReviewHookContext;

        public List<TestHookContext> Contexts { get; } = new();

        public IReviewHookContext CreateProjectCreated(ReviewProject project)
        {
            return Record("project.created", new Dictionary<string, string>
            {
                ["projectId"] = project.Id
            });
        }

        public IReviewHookContext CreateAssignmentUpdated(ReviewStage stage, ScreeningAssignment assignment)
        {
            return Record("assignment.updated", new Dictionary<string, string>
            {
                ["stageId"] = stage.Id,
                ["assignmentId"] = assignment.Id
            });
        }

        public IReviewHookContext CreateReviewerDecisionRecorded(ScreeningAssignment assignment, ReviewerDecision decision)
        {
            return Record("decision.recorded", new Dictionary<string, string>
            {
                ["assignmentId"] = assignment.Id,
                ["decision"] = decision.Decision.ToString()
            });
        }

        public IReviewHookContext CreateStageTransition(ReviewStage stage, ConflictState previousState, ConflictState currentState)
        {
            return Record("stage.transition", new Dictionary<string, string>
            {
                ["stageId"] = stage.Id,
                ["from"] = previousState.ToString(),
                ["to"] = currentState.ToString()
            });
        }

        private IReviewHookContext Record(string action, Dictionary<string, string> tags)
        {
            var context = new TestHookContext(action, tags);
            Contexts.Add(context);
            return context;
        }
    }

    private sealed class FakeHookOrchestrator : IReviewHookOrchestrator
    {
        public List<(string EntryId, FakeHookContextFactory.TestHookContext Context)> Calls { get; } = new();

        public Task ProcessAsync(string entryId, IReviewHookContext context, CancellationToken cancellationToken)
        {
            Calls.Add((entryId, (FakeHookContextFactory.TestHookContext)context));
            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _current;

        public TestTimeProvider(DateTimeOffset initial)
        {
            _current = initial;
        }

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan delta)
        {
            _current = _current.Add(delta);
        }
    }
}
