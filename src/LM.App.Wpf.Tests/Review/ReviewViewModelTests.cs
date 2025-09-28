using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Services;
using LM.App.Wpf.Services.Review;
using LM.App.Wpf.ViewModels.Review;
using LM.Core.Abstractions;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;
using Xunit;

namespace LM.App.Wpf.Tests.Review
{
    public sealed class ReviewViewModelTests : IDisposable
    {
        private readonly ReviewViewModelFixture _fixture;

        public ReviewViewModelTests()
        {
            _fixture = new ReviewViewModelFixture();
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public async Task InitializeAsync_LoadsProjectsAndClearsSelection()
        {
            await _fixture.ViewModel.InitializeAsync(CancellationToken.None);

            Assert.Equal(2, _fixture.ViewModel.Projects.Count);
            Assert.Null(_fixture.ViewModel.SelectedProject);
            Assert.Empty(_fixture.ViewModel.Stages);
            Assert.Empty(_fixture.ViewModel.Assignments);
        }

        [Fact]
        public async Task SelectProjectCommand_DisablesDuringExecution()
        {
            using var gatedFixture = new ReviewViewModelFixture(delayProjectLoad: true);
            var viewModel = gatedFixture.ViewModel;

            Assert.True(viewModel.SelectProjectCommand.CanExecute(gatedFixture.Project1.Id));

            var selectTask = viewModel.SelectProjectAsync(gatedFixture.Project1.Id, CancellationToken.None);

            await gatedFixture.ProjectGate!.WaitForSubscribersAsync();
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.SelectProjectCommand.CanExecute(gatedFixture.Project1.Id));

            gatedFixture.ReleaseProjectGate();
            await selectTask;

            Assert.False(viewModel.IsBusy);
            Assert.True(viewModel.SelectProjectCommand.CanExecute(gatedFixture.Project1.Id));
        }

        [Fact]
        public async Task NavigateStageAsync_UpdatesChildViewModels()
        {
            await _fixture.ViewModel.SelectProjectAsync(_fixture.Project1.Id, CancellationToken.None);
            await _fixture.ViewModel.NavigateStageAsync(_fixture.Stage1.Id, CancellationToken.None);

            Assert.Equal(_fixture.Stage1.Id, _fixture.ViewModel.SelectedStage?.Id);
            Assert.Equal(2, _fixture.ViewModel.Assignments.Count);
            Assert.Equal(_fixture.Stage1.Id, _fixture.ViewModel.ScreeningQueue.Stage?.Id);
            Assert.NotNull(_fixture.ViewModel.AssignmentDetail.SelectedAssignment);
            Assert.True(_fixture.ViewModel.ExtractionWorkspace.IsActive);
            Assert.Equal(_fixture.Stage1.ConflictState, _fixture.ViewModel.QualityAssurance.ConflictState);
            Assert.Equal(_fixture.Project1.Id, _fixture.ViewModel.Analytics.ProjectSnapshot?.ProjectId);
            Assert.Equal(_fixture.Stage1.Id, _fixture.ViewModel.Analytics.Stage?.Id);
        }

        [Fact]
        public async Task RefreshCommand_RehydratesStageAssignments()
        {
            await _fixture.ViewModel.SelectProjectAsync(_fixture.Project1.Id, CancellationToken.None);
            await _fixture.ViewModel.NavigateStageAsync(_fixture.Stage1.Id, CancellationToken.None);

            _fixture.Store.OverrideAssignments(_fixture.Stage1.Id, new[]
            {
                _fixture.CreateAssignment("a3", ScreeningStatus.Pending)
            });

            await _fixture.ViewModel.RefreshAsync(CancellationToken.None);

            Assert.Single(_fixture.ViewModel.Assignments);
            Assert.Equal("a3", _fixture.ViewModel.Assignments[0].Id);
        }

        [Fact]
        public async Task NavigateStageAsync_ThrowsWhenStageMissing()
        {
            await _fixture.ViewModel.SelectProjectAsync(_fixture.Project1.Id, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _fixture.ViewModel.NavigateStageAsync("missing", CancellationToken.None));
        }

        [Fact]
        public async Task SelectProjectAsync_ValidatesInput()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _fixture.ViewModel.SelectProjectAsync(" ", CancellationToken.None));
        }

        [Fact]
        public async Task CreateProjectAsync_SelectsCreatedProject()
        {
            var newProject = ReviewProject.Create(
                "project-new",
                "Project New",
                DateTimeOffset.UtcNow,
                new[] { _fixture.Definition });
            _fixture.Launcher.ProjectToCreate = newProject;

            await _fixture.ViewModel.CreateProjectAsync(CancellationToken.None);

            Assert.Equal(newProject.Id, _fixture.ViewModel.SelectedProject?.Id);
            Assert.Contains(_fixture.ViewModel.Projects, project => project.Id == newProject.Id);
        }

        [Fact]
        public async Task CreateProjectAsync_NoSelectionWhenCancelled()
        {
            _fixture.Launcher.ProjectToCreate = null;

            await _fixture.ViewModel.CreateProjectAsync(CancellationToken.None);

            Assert.Null(_fixture.ViewModel.SelectedProject);
        }

        [Fact]
        public async Task LoadProjectAsync_SelectsLoadedProject()
        {
            await _fixture.ViewModel.InitializeAsync(CancellationToken.None);
            _fixture.Launcher.ProjectToLoad = _fixture.Project1;

            await _fixture.ViewModel.LoadProjectAsync(CancellationToken.None);

            Assert.Equal(_fixture.Project1.Id, _fixture.ViewModel.SelectedProject?.Id);
        }

        private sealed class ReviewViewModelFixture : IDisposable
        {
            private readonly TempWorkspace _workspace;
            private readonly TestUserContext _userContext;
            private readonly HookOrchestrator _orchestrator;
            private readonly FakeReviewWorkflowStore _store;
            private readonly FakeWorkflowService _workflowService;
            private readonly FakeReviewProjectLauncher _launcher;
            private readonly ProjectDashboardViewModel _dashboard;
            private readonly ScreeningQueueViewModel _queue;
            private readonly AssignmentDetailViewModel _assignmentDetail;
            private readonly ExtractionWorkspaceViewModel _extraction;
            private readonly QualityAssuranceViewModel _quality;
            private readonly AnalyticsViewModel _analytics;
            private readonly string _stage1Id = "stage-1";
            private readonly StageDefinition _definition;

            public ReviewViewModelFixture(bool delayProjectLoad = false)
            {
                _workspace = new TempWorkspace();
                _userContext = new TestUserContext("unit-test");
                _orchestrator = new HookOrchestrator(_workspace);
                var analyticsService = new ReviewAnalyticsService();
                ProjectGate = delayProjectLoad ? new AsyncGate() : null;
                _store = new FakeReviewWorkflowStore(ProjectGate);
                _workflowService = new FakeWorkflowService(_store);

                var requirement = ReviewerRequirement.Create(new[]
                {
                    new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1),
                    new KeyValuePair<ReviewerRole, int>(ReviewerRole.Secondary, 1)
                });
                var policy = StageConsensusPolicy.Disabled();
                var screeningDisplay = StageDisplayProfile.Create(new[]
                {
                    StageContentArea.BibliographySummary,
                    StageContentArea.InclusionExclusionChecklist,
                    StageContentArea.ReviewerDecisionPanel
                });
                _definition = StageDefinition.Create(
                    "def-1",
                    "Screening",
                    ReviewStageType.TitleScreening,
                    requirement,
                    policy,
                    screeningDisplay);
                Project1 = ReviewProject.Create("project-1", "Project One", DateTimeOffset.UtcNow, new[] { _definition });
                var assignments = new List<ScreeningAssignment>
                {
                    CreateAssignment("a1", ScreeningStatus.Pending),
                    CreateAssignment("a2", ScreeningStatus.Included)
                };

                Stage1 = ReviewStage.Create(
                    _stage1Id,
                    Project1.Id,
                    _definition,
                    assignments,
                    ConflictState.None,
                    DateTimeOffset.UtcNow);

                _store.Seed(Project1, Stage1, assignments);

                var qualityDisplay = StageDisplayProfile.Create(new[]
                {
                    StageContentArea.BibliographySummary,
                    StageContentArea.ReviewerDecisionPanel,
                    StageContentArea.NotesPane
                });
                var definition2 = StageDefinition.Create(
                    "def-2",
                    "QA",
                    ReviewStageType.QualityAssurance,
                    requirement,
                    policy,
                    qualityDisplay);
                var project2 = ReviewProject.Create("project-2", "Project Two", DateTimeOffset.UtcNow, new[] { definition2 });
                var stage2Assignments = new List<ScreeningAssignment>
                {
                    ScreeningAssignment.Create("b1", "stage-2", "reviewer-3", ReviewerRole.Primary, ScreeningStatus.Pending, DateTimeOffset.UtcNow),
                    ScreeningAssignment.Create("b2", "stage-2", "reviewer-4", ReviewerRole.Secondary, ScreeningStatus.Pending, DateTimeOffset.UtcNow)
                };
                var stage2 = ReviewStage.Create(
                    "stage-2",
                    project2.Id,
                    definition2,
                    stage2Assignments,
                    ConflictState.None,
                    DateTimeOffset.UtcNow);
                _store.Seed(project2, stage2, stage2Assignments);

                _dashboard = new ProjectDashboardViewModel(analyticsService, _orchestrator, _userContext);
                _queue = new ScreeningQueueViewModel(_orchestrator, _userContext);
                _assignmentDetail = new AssignmentDetailViewModel(_workflowService, _orchestrator, _userContext);
                _extraction = new ExtractionWorkspaceViewModel(_orchestrator, _userContext);
                _quality = new QualityAssuranceViewModel(_orchestrator, _userContext);
                _analytics = new AnalyticsViewModel(analyticsService, _orchestrator, _userContext);
                _launcher = new FakeReviewProjectLauncher(_store);

                ViewModel = new ReviewViewModel(
                    _store,
                    _orchestrator,
                    _userContext,
                    _dashboard,
                    _queue,
                    _assignmentDetail,
                    _extraction,
                    _quality,
                    _analytics,
                    _launcher);
            }

            public ReviewProject Project1 { get; }

            public ReviewStage Stage1 { get; }

            public FakeReviewProjectLauncher Launcher => _launcher;

            public StageDefinition Definition => _definition;

            public ReviewViewModel ViewModel { get; }

            public FakeReviewWorkflowStore Store => _store;

            public AsyncGate? ProjectGate { get; }

            public ScreeningAssignment CreateAssignment(string id, ScreeningStatus status)
            {
                var assignedAt = DateTimeOffset.UtcNow;
                if (status is ScreeningStatus.Included or ScreeningStatus.Excluded)
                {
                    var decision = ReviewerDecision.Create(id, "reviewer-2", status, assignedAt);
                    return ScreeningAssignment.Create(id, _stage1Id, "reviewer-2", ReviewerRole.Secondary, status, assignedAt, assignedAt, decision);
                }

                return ScreeningAssignment.Create(id, _stage1Id, "reviewer-1", ReviewerRole.Primary, status, assignedAt);
            }

            public void ReleaseProjectGate()
            {
                ProjectGate?.Release();
            }

            public void Dispose()
            {
                _workspace.Dispose();
            }
        }

        private sealed class FakeReviewProjectLauncher : IReviewProjectLauncher
        {
            private readonly FakeReviewWorkflowStore _store;

            public FakeReviewProjectLauncher(FakeReviewWorkflowStore store)
            {
                _store = store;
            }

            public ReviewProject? ProjectToCreate { get; set; }

            public ReviewProject? ProjectToLoad { get; set; }

            public Task<ReviewProject?> CreateProjectAsync(CancellationToken cancellationToken)
            {
                if (ProjectToCreate is not null)
                {
                    _store.AddProject(ProjectToCreate);
                }

                return Task.FromResult(ProjectToCreate);
            }

            public Task<ReviewProject?> LoadProjectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(ProjectToLoad);
            }
        }

        private sealed class FakeReviewWorkflowStore : IReviewWorkflowStore
        {
            private readonly Dictionary<string, ReviewProject> _projects = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<ReviewStage>> _stagesByProject = new(StringComparer.Ordinal);
            private readonly Dictionary<string, ReviewStage> _stages = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<ScreeningAssignment>> _assignmentsByStage = new(StringComparer.Ordinal);
            private readonly AsyncGate? _gate;

            public FakeReviewWorkflowStore(AsyncGate? gate)
            {
                _gate = gate;
            }

            public void Seed(ReviewProject project, ReviewStage stage, IEnumerable<ScreeningAssignment> assignments)
            {
                _projects[project.Id] = project;
                if (!_stagesByProject.TryGetValue(project.Id, out var stages))
                {
                    stages = new List<ReviewStage>();
                    _stagesByProject[project.Id] = stages;
                }

                var existingIndex = stages.FindIndex(s => s.Id == stage.Id);
                if (existingIndex >= 0)
                {
                    stages[existingIndex] = stage;
                }
                else
                {
                    stages.Add(stage);
                }

                _stages[stage.Id] = stage;
                _assignmentsByStage[stage.Id] = assignments.ToList();
            }

            public void AddProject(ReviewProject project)
            {
                _projects[project.Id] = project;
                _stagesByProject.TryAdd(project.Id, new List<ReviewStage>());
            }

            public void OverrideAssignments(string stageId, IEnumerable<ScreeningAssignment> assignments)
            {
                var snapshot = assignments.ToList();
                _assignmentsByStage[stageId] = snapshot;

                if (_stages.TryGetValue(stageId, out var existing))
                {
                    var updated = ReviewStage.Create(
                        existing.Id,
                        existing.ProjectId,
                        existing.Definition,
                        snapshot,
                        existing.ConflictState,
                        existing.ActivatedAt,
                        existing.CompletedAt,
                        existing.Consensus);

                    _stages[stageId] = updated;

                    if (_stagesByProject.TryGetValue(existing.ProjectId, out var stages))
                    {
                        var index = stages.FindIndex(s => s.Id == stageId);
                        if (index >= 0)
                        {
                            stages[index] = updated;
                        }
                    }
                }
            }

            public Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
            {
                return AwaitGateAsync(() => _projects.TryGetValue(projectId, out var project) ? project : null, cancellationToken);
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
                _stagesByProject.TryGetValue(projectId, out var stages);
                IReadOnlyList<ReviewStage> snapshot = stages is null
                    ? Array.Empty<ReviewStage>()
                    : stages.ToList();
                return Task.FromResult(snapshot);
            }

            public Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
            {
                foreach (var assignments in _assignmentsByStage.Values)
                {
                    var match = assignments.FirstOrDefault(a => a.Id == assignmentId);
                    if (match is not null)
                    {
                        return Task.FromResult<ScreeningAssignment?>(match);
                    }
                }

                return Task.FromResult<ScreeningAssignment?>(null);
            }

            public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
            {
                _assignmentsByStage.TryGetValue(stageId, out var assignments);
                IReadOnlyList<ScreeningAssignment> snapshot = assignments is null
                    ? Array.Empty<ScreeningAssignment>()
                    : assignments.ToList();
                return Task.FromResult(snapshot);
            }

            public Task SaveProjectAsync(ReviewProject project, CancellationToken cancellationToken)
            {
                AddProject(project);
                return Task.CompletedTask;
            }

            public Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
            {
                _stages[stage.Id] = stage;
                if (_stagesByProject.TryGetValue(stage.ProjectId, out var stages))
                {
                    var index = stages.FindIndex(s => s.Id == stage.Id);
                    if (index >= 0)
                    {
                        stages[index] = stage;
                    }
                }

                return Task.CompletedTask;
            }

            public Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
            {
                if (_assignmentsByStage.TryGetValue(assignment.StageId, out var assignments))
                {
                    var index = assignments.FindIndex(a => a.Id == assignment.Id);
                    if (index >= 0)
                    {
                        assignments[index] = assignment;
                    }
                    else
                    {
                        assignments.Add(assignment);
                    }
                }
                else
                {
                    _assignmentsByStage[assignment.StageId] = new List<ScreeningAssignment> { assignment };
                }

                return Task.CompletedTask;
            }

            private async Task<T?> AwaitGateAsync<T>(Func<T?> resolver, CancellationToken cancellationToken)
            {
                if (_gate is not null)
                {
                    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                return resolver();
            }
        }

        private sealed class FakeWorkflowService : IReviewWorkflowService
        {
            private readonly FakeReviewWorkflowStore _store;

            public FakeWorkflowService(FakeReviewWorkflowStore store)
            {
                _store = store;
            }

            public Task<ReviewStage> CreateStageAsync(string projectId, string stageDefinitionId, IReadOnlyCollection<ReviewerAssignmentRequest> assignments, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public async Task<ScreeningAssignment> SubmitDecisionAsync(string assignmentId, ScreeningStatus decision, string? notes, CancellationToken cancellationToken = default)
            {
                var assignment = await _store.GetAssignmentAsync(assignmentId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Assignment '{assignmentId}' not found.");

                var decidedAt = DateTimeOffset.UtcNow;
                var reviewerDecision = ReviewerDecision.Create(assignmentId, assignment.ReviewerId, decision, decidedAt, notes);
                var updated = ScreeningAssignment.Create(
                    assignment.Id,
                    assignment.StageId,
                    assignment.ReviewerId,
                    assignment.Role,
                    decision,
                    assignment.AssignedAt,
                    decidedAt,
                    reviewerDecision);

                await _store.SaveAssignmentAsync(string.Empty, updated, cancellationToken).ConfigureAwait(false);
                return updated;
            }
        }

        private sealed class TestUserContext : IUserContext
        {
            public TestUserContext(string userName)
            {
                UserName = userName;
            }

            public string UserName { get; }
        }

        private sealed class TempWorkspace : IWorkSpaceService, IDisposable
        {
            private readonly string _root;

            public TempWorkspace()
            {
                _root = Path.Combine(Path.GetTempPath(), "review_vm_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);
            }

            public string? WorkspacePath => _root;

            public string GetWorkspaceRoot() => _root;

            public string GetLocalDbPath() => Path.Combine(_root, "local.db");

            public string GetAbsolutePath(string relativePath)
            {
                return Path.Combine(_root, relativePath);
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_root, recursive: true);
                }
                catch
                {
                }
            }
        }

        private sealed class AsyncGate
        {
            private readonly TaskCompletionSource<bool> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _waiterCount;

            public Task WaitAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _waiterCount);
                cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), _source);
                return _source.Task;
            }

            public Task WaitForSubscribersAsync()
            {
                SpinWait.SpinUntil(() => Volatile.Read(ref _waiterCount) > 0, TimeSpan.FromSeconds(1));
                return Task.CompletedTask;
            }

            public void Release()
            {
                _source.TrySetResult(true);
            }
        }
    }
}
