using System;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using Xunit;

namespace LM.App.Wpf.Tests.Dialogs.Staging
{
    public sealed class DataExtractionWorkspaceViewModelTests : IDisposable
    {
        private readonly WorkspaceService _workspace;
        private readonly HookOrchestrator _orchestrator;

        public DataExtractionWorkspaceViewModelTests()
        {
            _workspace = new WorkspaceService();
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw_workspace_" + Guid.NewGuid().ToString("N"));
            _workspace.EnsureWorkspaceAsync(root).GetAwaiter().GetResult();
            _orchestrator = new HookOrchestrator(_workspace);
        }

        [Fact]
        public async Task SaveAsync_Populates_Hook_And_ChangeLog()
        {
            var item = new StagingItem
            {
                FilePath = "/tmp/sample.pdf",
                DataExtractionHook = new LM.HubSpoke.Models.DataExtractionHook()
            };

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator);

            viewModel.AddTableCommand.Execute(null);
            Assert.NotNull(viewModel.SelectedAsset);

            viewModel.SelectedAsset!.ColumnHint = 3;
            viewModel.SelectedAsset.DictionaryPath = "baseline.dictionary";
            viewModel.StudyDetails.StudyDesign = viewModel.StudyDetails.StudyDesignOptions[0];

            await viewModel.SaveAsyncCommand.ExecuteAsync(null);

            Assert.Single(item.DataExtractionHook!.Tables);
            Assert.Equal(3, item.DataExtractionHook.Tables[0].ColumnCountHint);
            Assert.Equal("baseline.dictionary", item.DataExtractionHook.Tables[0].DictionaryPath);
            Assert.Single(item.PendingChangeLogEvents);
            Assert.Equal(viewModel.StudyDetails.StudyDesign, item.DataExtractionHook.StudyDesign);
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
    }
}
