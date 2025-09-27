#nullable enable
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
        public async Task CreateProjectAsync_ShowsDialog_WhenSaveProjectFailsAfterCheckedEntryLoad()
        {
            var checkedEntriesPath = Path.Combine(Path.GetTempPath(), $"checked-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(
                checkedEntriesPath,
                "{\"checkedEntries\":{\"entryIds\":[\"lit-entry-1\"]}}",
                CancellationToken.None);

            var selection = new LitSearchRunSelection(
                "entry-1",
                "hooks/run.json",
                "hooks/run.json",
                "run-1",
                checkedEntriesPath,
                "hooks/checked.json",
                Array.Empty<string>());

            var messageBox = new FakeMessageBoxService();
            using var workspace = new FakeWorkSpaceService();
            var launcher = new ReviewProjectLauncher(
                new FakeDialogService(),
                new FakeEntryStore(new Entry
                {
                    Id = selection.EntryId,
                    Title = "Sample Entry"
                }),
                new ThrowingWorkflowStore(),
                new FakeReviewHookContextFactory(),
                new FakeReviewHookOrchestrator(),
                new HookOrchestrator(workspace),
                new FakeUserContext("tester"),
                workspace,
                new FakeRunPicker(selection),
                messageBox);

            try
            {
                var result = await launcher.CreateProjectAsync(CancellationToken.None);

                Assert.Null(result);

                var invocation = Assert.Single(messageBox.Invocations);
                Assert.Equal("Create review project", invocation.Caption);
                Assert.Equal(System.Windows.MessageBoxButton.OK, invocation.Buttons);
                Assert.Equal(System.Windows.MessageBoxImage.Error, invocation.Image);
                Assert.Equal("Review project save failed", invocation.Message);
            }
            finally
            {
                if (File.Exists(checkedEntriesPath))
                {
                    File.Delete(checkedEntriesPath);
                }
            }
        }

        [Fact]
        public async Task CreateProjectAsync_UsesRelativeCheckedEntriesPath_WhenAbsoluteMissing()
        {
            var workspaceRoot = Path.Combine(Path.GetTempPath(), $"kw-review-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspaceRoot);

            try
            {
                var relativePath = Path.Combine("entries", "entry-1", "hooks", "litsearch_run_seed_checked.json");
                var absolutePath = Path.Combine(workspaceRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                await File.WriteAllTextAsync(
                    absolutePath,
                    "{\"checkedEntries\":{\"entryIds\":[\"lit-entry-1\",\"lit-entry-2\"]}}",
                    CancellationToken.None);

                var selection = new LitSearchRunSelection(
                    "entry-1",
                    "hooks/litsearch.json",
                    "hooks/litsearch.json",
                    "run-1",
                    null,
                    relativePath.Replace('\\', '/'),
                    Array.Empty<string>());

                using var workspace = new FakeWorkSpaceService(workspaceRoot);
                var workflowStore = new RecordingWorkflowStore();
                var changeLogOrchestrator = new HookOrchestrator(workspace);
                var launcher = new ReviewProjectLauncher(
                    new FakeDialogService(),
                    new FakeEntryStore(new Entry
                    {
                        Id = selection.EntryId,
                        Title = "Seed Entry"
                    }),
                    workflowStore,
                    new FakeReviewHookContextFactory(),
                    new FakeReviewHookOrchestrator(),
                    changeLogOrchestrator,
                    new FakeUserContext("tester"),
                    workspace,
                    new FakeRunPicker(selection),
                    new FakeMessageBoxService());

                var project = await launcher.CreateProjectAsync(CancellationToken.None);

                Assert.NotNull(project);
                Assert.NotNull(workflowStore.SavedProject);

                var changeLogPath = Path.Combine(
                    workspaceRoot,
                    "entries",
                    project!.Id,
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
                    // Best effort cleanup.
                }
            }
        }

        private sealed class FakeDialogService : IDialogService
        {
            public string[]? ShowOpenFileDialog(FilePickerOptions options) => null;

            public string? ShowFolderBrowserDialog(FolderPickerOptions options) => null;

            public bool? ShowStagingEditor(LM.App.Wpf.ViewModels.StagingListViewModel stagingList) => null;
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
                    // best effort cleanup
                }
            }
        }

        private sealed class RecordingWorkflowStore : IReviewWorkflowStore
        {
            private ReviewProject? _project;

            public ReviewProject? SavedProject => _project;

            public Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
                => Task.FromResult(_project);

            public Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ReviewProject>>(_project is null
                    ? Array.Empty<ReviewProject>()
                    : new[] { _project });

            public Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken cancellationToken)
                => Task.FromResult<ReviewStage?>(null);

            public Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ReviewStage>>(Array.Empty<ReviewStage>());

            public Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken cancellationToken)
                => Task.FromResult<ScreeningAssignment?>(null);

            public Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<ScreeningAssignment>>(Array.Empty<ScreeningAssignment>());

            public Task SaveProjectAsync(ReviewProject project, CancellationToken cancellationToken)
            {
                _project = project;
                return Task.CompletedTask;
            }

            public Task SaveStageAsync(ReviewStage stage, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken cancellationToken)
                => Task.CompletedTask;
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
