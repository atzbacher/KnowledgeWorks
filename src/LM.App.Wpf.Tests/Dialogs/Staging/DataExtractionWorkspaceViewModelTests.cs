using System;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Core.Utils;
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

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator, NullDataExtractionPreprocessor.Instance);

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

        [Fact]
        public void ViewerRegionCommandsUpdateSelectedAsset()
        {
            var item = new StagingItem
            {
                FilePath = "/tmp/sample.pdf"
            };

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator, NullDataExtractionPreprocessor.Instance);
            viewModel.AddTableCommand.Execute(null);

            var asset = Assert.IsType<DataExtractionAssetViewModel>(viewModel.SelectedAsset);

            var draft = new PdfRegionDraft(2, 0.1, 0.2, 0.3, 0.4);
            viewModel.CreateRegionFromViewerCommand.Execute(draft);

            Assert.Single(asset.Regions);
            var region = asset.Regions[0];
            Assert.Equal(2, region.PageNumber);
            Assert.Equal(0.1, region.X, 3);
            Assert.Equal(0.2, region.Y, 3);
            Assert.Equal(0.3, region.Width, 3);
            Assert.Equal(0.4, region.Height, 3);
            Assert.Same(region, viewModel.SelectedRegion);
            Assert.Equal(2, viewModel.CurrentPage);

            var update = new PdfRegionUpdate(region, 3, 0.2, 0.25, 0.5, 0.6);
            viewModel.UpdateRegionFromViewerCommand.Execute(update);

            Assert.Equal(3, region.PageNumber);
            Assert.Equal(0.2, region.X, 3);
            Assert.Equal(0.25, region.Y, 3);
            Assert.Equal(0.5, region.Width, 3);
            Assert.Equal(0.6, region.Height, 3);
            Assert.Same(region, viewModel.SelectedRegion);
            Assert.Equal(3, viewModel.CurrentPage);
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
