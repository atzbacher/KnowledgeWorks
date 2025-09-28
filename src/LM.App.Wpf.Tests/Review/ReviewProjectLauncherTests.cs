using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Services;
using LM.App.Wpf.Services.Review;
using LM.App.Wpf.ViewModels.Review;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Review.Core.Models;
using LM.Review.Core.Services;
using Xunit;

namespace LM.App.Wpf.Tests.Review
{
    public sealed class ReviewProjectLauncherTests
    {
        [Fact]
        public async Task CreateProjectAsync_ReturnsNull_WhenEditorCancels()
        {
            var selection = new LitSearchRunSelection(
                "entry-1",
                "hooks/run.json",
                "hooks/run.json",
                "run-1",
                null,
                null,
                new List<string>());

            using var workspace = new FakeWorkSpaceService();
            var workflowStore = new RecordingWorkflowStore();
            var dialogService = new FakeDialogService
            {
                ProjectEditorResult = false,
                OnShowProjectEditor = static _ => { }
            };

            var launcher = CreateLauncher(
                dialogService,
                new FakeEntryStore(new Entry { Id = selection.EntryId, Title = "Seed" }),
                workflowStore,
                workspace,
                new FakeRunPicker(selection),
                new FakeMessageBoxService());

            var project = await launcher.CreateProjectAsync(CancellationToken.None);

            Assert.Null(project);
            Assert.Null(workflowStore.SavedProject);
            Assert.NotNull(dialogService.LastEditor);
        }

        [Fact]
        public async Task CreateProjectAsync_PersistsProject_WhenEditorCompletes()
        {
            var selection = new LitSearchRunSelection(
                "entry-1",
                "hooks/run.json",
                "hooks/run.json",
                "run-1",
                null,
                null,
                new List<string> { "lit-entry-1" });

            using var workspace = new FakeWorkSpaceService();
            var workflowStore = new RecordingWorkflowStore();
            var changeLogOrchestrator = new HookOrchestrator(workspace);
            var dialogService = new FakeDialogService();

            var launcher = CreateLauncher(
                dialogService,
                new FakeEntryStore(new Entry { Id = selection.EntryId, Title = "Seed" }),
                workflowStore,
                workspace,
                new FakeRunPicker(selection),
                new FakeMessageBoxService(),
                changeLogOrchestrator);

            var project = await launcher.CreateProjectAsync(CancellationToken.None);

            Assert.NotNull(project);
            Assert.NotNull(workflowStore.SavedProject);
            Assert.Equal(project!.Id, workflowStore.SavedProject!.Id);
            Assert.Equal("Review – Seed", project.Name);
            Assert.Equal(ReviewTemplateKind.Picos, project.Metadata.Template);
            Assert.Equal(string.Empty, project.Metadata.Notes);

            var savedStages = workflowStore.GetStages(project.Id);
            Assert.Equal(project.StageDefinitions.Count, savedStages.Count);
            Assert.All(savedStages, stage => Assert.Equal(project.Id, stage.ProjectId));

            var savedAssignments = workflowStore.GetAssignments();
            var expectedAssignmentCount = project.StageDefinitions.Sum(definition => definition.ReviewerRequirement.TotalRequired);
            Assert.Equal(expectedAssignmentCount, savedAssignments.Count);
            Assert.All(savedAssignments, assignment => Assert.Contains(savedStages, stage => stage.Id == assignment.StageId));

            var changeLogPath = Path.Combine(
                workspace.GetWorkspaceRoot(),
                "entries",
                project.Id,
                "hooks",
                "changelog.json");
            Assert.True(File.Exists(changeLogPath));

            var json = await File.ReadAllTextAsync(changeLogPath, CancellationToken.None);
            using var document = JsonDocument.Parse(json);
            var tags = document.RootElement
                .GetProperty("events")[0]
                .GetProperty("details")
                .GetProperty("tags")
                .EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            Assert.Contains("litsearchEntryCount:1", tags);
            Assert.Contains("litsearchRun:run-1", tags);
            Assert.Contains("projectId:" + project.Id, tags);
            Assert.Contains("reviewTemplate:Picos", tags);
        }

        [Fact]
        public async Task CreateProjectAsync_UsesRelativeCheckedEntriesFile_WhenIdsMissing()
        {
            var workspaceRoot = Path.Combine(Path.GetTempPath(), $"kw-review-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspaceRoot);

            try
            {
                var relativePath = Path.Combine("entries", "entry-1", "hooks", "litsearch_checked.json");
                var absolutePath = Path.Combine(workspaceRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                await File.WriteAllTextAsync(
                    absolutePath,
                    "{\"checkedEntries\":{\"entryIds\":[\"lit-entry-1\",\"lit-entry-2\"]}}",
                    CancellationToken.None);

                var selection = new LitSearchRunSelection(
                    "entry-1",
                    "hooks/run.json",
                    "hooks/run.json",
                    "run-1",
                    null,
                    relativePath.Replace('\\', '/'),
                    Array.Empty<string>());

                using var workspace = new FakeWorkSpaceService(workspaceRoot);
                var workflowStore = new RecordingWorkflowStore();
                var changeLogOrchestrator = new HookOrchestrator(workspace);
                var dialogService = new FakeDialogService();

                var launcher = CreateLauncher(
                    dialogService,
                    new FakeEntryStore(new Entry { Id = selection.EntryId, Title = "Seed" }),
                    workflowStore,
                    workspace,
                    new FakeRunPicker(selection),
                    new FakeMessageBoxService(),
                    changeLogOrchestrator);

                var project = await launcher.CreateProjectAsync(CancellationToken.None);
                Assert.NotNull(project);

                var changeLogPath = Path.Combine(
                    workspaceRoot,
                    "entries",
                    project!.Id,
                    "hooks",
                    "changelog.json");
                var json = await File.ReadAllTextAsync(changeLogPath, CancellationToken.None);
                using var document = JsonDocument.Parse(json);
                var tags = document.RootElement
                    .GetProperty("events")[0]
                    .GetProperty("details")
                    .GetProperty("tags")
                    .EnumerateArray()
                    .Select(element => element.GetString())
                    .Where(value => value is not null)
                    .Select(value => value!)
                    .ToList();

                Assert.Contains("litsearchEntryCount:2", tags);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(workspaceRoot))
                    {
                        Directory.Delete(workspaceRoot, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }

        [Fact]
        public async Task CreateProjectAsync_ShowsError_WhenSaveFails()
        {
            var selection = new LitSearchRunSelection(
                "entry-1",
                "hooks/run.json",
                "hooks/run.json",
                "run-1",
                null,
                null,
                new List<string>());

            using var workspace = new FakeWorkSpaceService();
            var dialogService = new FakeDialogService();
            var messageBox = new FakeMessageBoxService();

            var launcher = CreateLauncher(
                dialogService,
                new FakeEntryStore(new Entry { Id = selection.EntryId, Title = "Seed" }),
                new ThrowingWorkflowStore(),
                workspace,
                new FakeRunPicker(selection),
                messageBox);

            var project = await launcher.CreateProjectAsync(CancellationToken.None);

            Assert.Null(project);
            var invocation = Assert.Single(messageBox.Invocations);
            Assert.Equal("Create review project", invocation.Caption);
            Assert.Equal(System.Windows.MessageBoxButton.OK, invocation.Buttons);
            Assert.Equal(System.Windows.MessageBoxImage.Error, invocation.Image);
        }

        private static ReviewProjectLauncher CreateLauncher(
            FakeDialogService dialogService,
            IEntryStore entryStore,
            IReviewWorkflowStore workflowStore,
            FakeWorkSpaceService workspace,
            ILitSearchRunPicker runPicker,
            IMessageBoxService messageBoxService,
            HookOrchestrator? changeLogOrchestrator = null)
        {
            changeLogOrchestrator ??= new HookOrchestrator(workspace);
            return new ReviewProjectLauncher(
                dialogService,
                entryStore,
                workflowStore,
                new FakeReviewHookContextFactory(),
                new FakeReviewHookOrchestrator(),
                changeLogOrchestrator,
                new FakeUserContext("tester"),
                workspace,
                runPicker,
                messageBoxService,
                new FakeReviewCreationDiagnostics(),
                () => new ProjectEditorViewModel());
        }

        private sealed class FakeDialogService : IDialogService
        {
            public ProjectEditorViewModel? LastEditor { get; private set; }

            public bool? ProjectEditorResult { get; set; } = true;

            public Action<ProjectEditorViewModel>? OnShowProjectEditor { get; set; }
                = static editor => editor.SaveCommand.Execute(null);

            public string[]? ShowOpenFileDialog(FilePickerOptions options) => null;

            public string? ShowFolderBrowserDialog(FolderPickerOptions options) => null;

            public bool? ShowStagingEditor(LM.App.Wpf.ViewModels.StagingListViewModel stagingList) => null;

            public bool? ShowProjectEditor(ProjectEditorViewModel editor)
            {
                LastEditor = editor;
                OnShowProjectEditor?.Invoke(editor);
                return ProjectEditorResult;
            }
        }

        private sealed class FakeEntryStore : IEntryStore
        {
            private readonly Entry? _entry;

            public FakeEntryStore(Entry? entry)
            {
                _entry = entry;
            }

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default) => Task.CompletedTask;

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default) => Task.FromResult(_entry);

            public async IAsyncEnumerable<Entry> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(LM.Core.Models.Filters.EntryFilter filter, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);
        }

        private sealed class ThrowingWorkflowStore : IReviewWorkflowStore
        {
            public Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
                => Task.FromResult<ReviewProject?>(null);

            public Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ReviewProject>>(Array.Empty<ReviewProject>());

            public Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken)
                => Task.FromResult<ReviewStage?>(null);

            public Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ReviewStage>>(Array.Empty<ReviewStage>());

            public Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
                => Task.FromResult<ScreeningAssignment?>(null);

            public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ScreeningAssignment>>(Array.Empty<ScreeningAssignment>());

            public Task SaveProjectAsync(ReviewProject project, CancellationToken cancellationToken)
                => Task.FromException(new InvalidOperationException("Review project save failed"));

            public Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class RecordingWorkflowStore : IReviewWorkflowStore
        {
            private readonly Dictionary<string, ReviewProject> _projects = new(StringComparer.Ordinal);
            private readonly Dictionary<string, ReviewStage> _stages = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<string>> _stageIdsByProject = new(StringComparer.Ordinal);
            private readonly Dictionary<string, ScreeningAssignment> _assignments = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<string>> _assignmentIdsByStage = new(StringComparer.Ordinal);

            public ReviewProject? SavedProject { get; private set; }

            public Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
                => Task.FromResult(_projects.TryGetValue(projectId, out var project) ? project : null);

            public Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ReviewProject>>(_projects.Values.ToList());

            public Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken)
                => Task.FromResult(_stages.TryGetValue(stageId, out var stage) ? stage : null);

            public Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken)
            {
                if (!_stageIdsByProject.TryGetValue(projectId, out var ids) || ids.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<ReviewStage>>(Array.Empty<ReviewStage>());
                }

                var stages = ids.Select(id => _stages[id]).ToList();
                return Task.FromResult<IReadOnlyList<ReviewStage>>(stages);
            }

            public Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
                => Task.FromResult(_assignments.TryGetValue(assignmentId, out var assignment) ? assignment : null);

            public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
            {
                if (!_assignmentIdsByStage.TryGetValue(stageId, out var ids) || ids.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<ScreeningAssignment>>(Array.Empty<ScreeningAssignment>());
                }

                var assignments = ids.Select(id => _assignments[id]).ToList();
                return Task.FromResult<IReadOnlyList<ScreeningAssignment>>(assignments);
            }

            public Task SaveProjectAsync(ReviewProject project, CancellationToken cancellationToken)
            {
                SavedProject = project;
                _projects[project.Id] = project;
                _stageIdsByProject.TryAdd(project.Id, new List<string>());
                return Task.CompletedTask;
            }

            public Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
            {
                _stages[stage.Id] = stage;
                if (!_stageIdsByProject.TryGetValue(stage.ProjectId, out var ids))
                {
                    ids = new List<string>();
                    _stageIdsByProject[stage.ProjectId] = ids;
                }

                if (!ids.Contains(stage.Id, StringComparer.Ordinal))
                {
                    ids.Add(stage.Id);
                }

                _assignmentIdsByStage.TryAdd(stage.Id, new List<string>());
                return Task.CompletedTask;
            }

            public Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
            {
                _assignments[assignment.Id] = assignment;
                if (!_assignmentIdsByStage.TryGetValue(assignment.StageId, out var ids))
                {
                    ids = new List<string>();
                    _assignmentIdsByStage[assignment.StageId] = ids;
                }

                if (!ids.Contains(assignment.Id, StringComparer.Ordinal))
                {
                    ids.Add(assignment.Id);
                }

                return Task.CompletedTask;
            }

            public List<ReviewStage> GetStages(string projectId)
            {
                return GetStagesByProjectAsync(projectId, CancellationToken.None).GetAwaiter().GetResult().ToList();
            }

            public List<ScreeningAssignment> GetAssignments()
            {
                return _assignments.Values.ToList();
            }
        }

        private sealed class FakeReviewHookContextFactory : IReviewHookContextFactory
        {
            private sealed class FakeReviewHookContext : IReviewHookContext
            {
            }

            public IReviewHookContext CreateProjectCreated(ReviewProject project) => new FakeReviewHookContext();

            public IReviewHookContext CreateAssignmentUpdated(ReviewStage stage, ScreeningAssignment assignment)
                => new FakeReviewHookContext();

            public IReviewHookContext CreateReviewerDecisionRecorded(ScreeningAssignment assignment, ReviewerDecision decision)
                => new FakeReviewHookContext();

            public IReviewHookContext CreateStageTransition(ReviewStage stage, ConflictState previousState, ConflictState currentState)
                => new FakeReviewHookContext();
        }

        private sealed class FakeReviewHookOrchestrator : IReviewHookOrchestrator
        {
            public Task ProcessAsync(string entryId, IReviewHookContext context, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class FakeUserContext : IUserContext
        {
            public FakeUserContext(string userName)
            {
                UserName = userName;
            }

            public string UserName { get; }
        }

        private sealed class FakeReviewCreationDiagnostics : IReviewCreationDiagnostics
        {
            public List<string> Entries { get; } = new();

            public void RecordStep(string message)
            {
                Entries.Add($"INFO:{message}");
            }

            public void RecordException(string message, Exception exception)
            {
                Entries.Add($"ERROR:{message}:{exception.GetType().Name}");
            }
        }

        private sealed class FakeWorkSpaceService : IWorkSpaceService, IDisposable
        {
            private readonly string _root;
            private readonly bool _ownsRoot;

            public FakeWorkSpaceService(string? workspaceRoot = null)
            {
                if (string.IsNullOrWhiteSpace(workspaceRoot))
                {
                    _root = Path.Combine(Path.GetTempPath(), $"kw-review-{Guid.NewGuid():N}");
                    _ownsRoot = true;
                }
                else
                {
                    _root = workspaceRoot;
                    _ownsRoot = false;
                }

                Directory.CreateDirectory(_root);
            }

            public string? WorkspacePath => _root;

            public string GetWorkspaceRoot() => _root;

            public string GetLocalDbPath() => Path.Combine(GetWorkspaceRoot(), "workspace.db");

            public string GetAbsolutePath(string relativePath) => Path.Combine(GetWorkspaceRoot(), relativePath);

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
                => Task.CompletedTask;

            public void Dispose()
            {
                if (!_ownsRoot)
                {
                    return;
                }

                try
                {
                    if (Directory.Exists(_root))
                    {
                        Directory.Delete(_root, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }

        private sealed class FakeRunPicker : ILitSearchRunPicker
        {
            private readonly LitSearchRunSelection _selection;

            public FakeRunPicker(LitSearchRunSelection selection)
            {
                _selection = selection;
            }

            public Task<LitSearchRunSelection?> PickAsync(CancellationToken cancellationToken)
                => Task.FromResult<LitSearchRunSelection?>(_selection);
        }

        private sealed record MessageBoxInvocation(
            string Message,
            string Caption,
            System.Windows.MessageBoxButton Buttons,
            System.Windows.MessageBoxImage Image);

        private sealed class FakeMessageBoxService : IMessageBoxService
        {
            private readonly List<MessageBoxInvocation> _invocations = new();

            public IReadOnlyList<MessageBoxInvocation> Invocations => _invocations;

            public void Show(string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
            {
                _invocations.Add(new MessageBoxInvocation(message, caption, buttons, image));
            }
        }
    }
}
