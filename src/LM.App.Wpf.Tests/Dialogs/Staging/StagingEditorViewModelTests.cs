using System;
using System.Linq;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Core.Abstractions;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using Xunit;

namespace LM.App.Wpf.Tests.Dialogs.Staging
{
    public sealed class StagingEditorViewModelTests : IDisposable
    {
        private readonly WorkspaceService _workspace;
        private readonly HookOrchestrator _orchestrator;

        public StagingEditorViewModelTests()
        {
            _workspace = new WorkspaceService();
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw_editor_" + Guid.NewGuid().ToString("N"));
            _workspace.EnsureWorkspaceAsync(root).GetAwaiter().GetResult();
            _orchestrator = new HookOrchestrator(_workspace);
        }

        [Fact]
        public void Constructor_Builds_All_Tabs()
        {
            var list = new StagingListViewModel(new StubPipeline());
            var dialogService = new StubDialogService();
            var tables = new StagingTablesTabViewModel(_workspace, dialogService, _orchestrator);

            var editor = new StagingEditorViewModel(
                list,
                new StagingMetadataTabViewModel(list),
                tables,
                new StagingFiguresTabViewModel(),
                new StagingEndpointsTabViewModel(),
                new StagingPopulationTabViewModel(),
                new StagingReviewCommitTabViewModel(list, new DataExtractionCommitBuilder()),
                dialogService);

            Assert.Equal(6, editor.Tabs.Count);
            Assert.Equal("Metadata", editor.SelectedTab?.Header);

            var item = new StagingItem { Title = "Example" };
            list.Current = item;

            var metadata = editor.Tabs.OfType<StagingMetadataTabViewModel>().First();
            Assert.Equal(item, metadata.Current);

            editor.Dispose();
        }

        public void Dispose()
        {
            try
            {
                if (_workspace.WorkspacePath is not null && System.IO.Directory.Exists(_workspace.WorkspacePath))
                    System.IO.Directory.Delete(_workspace.WorkspacePath, recursive: true);
            }
            catch
            {
            }
        }

        private sealed class StubPipeline : IAddPipeline
        {
            public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<StagingItem>> StagePathsAsync(System.Collections.Generic.IEnumerable<string> paths, System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<StagingItem>>(Array.Empty<StagingItem>());

            public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<StagingItem>> CommitAsync(System.Collections.Generic.IEnumerable<StagingItem> selectedRows, System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<StagingItem>>(Array.Empty<StagingItem>());
        }

        private sealed class StubDialogService : IDialogService
        {
            public string[]? ShowOpenFileDialog(FilePickerOptions options) => Array.Empty<string>();
            public string? ShowFolderBrowserDialog(FolderPickerOptions options) => null;
            public string? ShowSaveFileDialog(FileSavePickerOptions options) => null;
            public bool? ShowStagingEditor(StagingListViewModel stagingList) => false;
            public bool? ShowDataExtractionWorkspace(StagingItem stagingItem) => true;
            public bool? ShowTabulaSharpPlayground(StagingItem stagingItem) => null;
        }
    }
}
