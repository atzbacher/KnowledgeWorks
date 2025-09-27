using System;
using System.Collections.Generic;
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
        public async Task SaveAsync_Preserves_Table_Metadata()
        {
            var table = new LM.HubSpoke.Models.DataExtractionTable
            {
                Id = "table-001",
                Title = "Baseline table",
                Caption = "Summary",
                Pages = new List<string> { "1", "2" },
                SourcePath = "tables/table.csv",
                ProvenanceHash = "doc-hash",
                FriendlyName = "Friendly Table",
                Notes = "Review required",
                ImagePath = "images/table.png",
                ImageProvenanceHash = "img-hash",
                ColumnCountHint = 4,
                DictionaryPath = "dict.json",
                LinkedEndpointIds = new List<string> { "endpoint-1", "endpoint-2" },
                LinkedInterventionIds = new List<string> { "intervention-1" },
                PagePositions = new List<LM.HubSpoke.Models.DataExtractionPagePosition>
                {
                    new LM.HubSpoke.Models.DataExtractionPagePosition
                    {
                        PageNumber = 3,
                        Left = 120.5,
                        Top = 240.25,
                        Width = 320.75,
                        Height = 180.5,
                        PageWidth = 612,
                        PageHeight = 792
                    }
                },
                Tags = new List<string> { "baseline" }
            };

            table.Regions.Add(new LM.HubSpoke.Models.DataExtractionRegion
            {
                PageNumber = 3,
                X = 0.1,
                Y = 0.2,
                Width = 0.5,
                Height = 0.4,
                Label = "Region 1"
            });

            var hook = new LM.HubSpoke.Models.DataExtractionHook
            {
                ExtractedAtUtc = DateTime.UtcNow.AddDays(-1),
                ExtractedBy = "tester",
                Tables = new List<LM.HubSpoke.Models.DataExtractionTable> { table },
                Figures = new List<LM.HubSpoke.Models.DataExtractionFigure>()
            };

            var item = new StagingItem
            {
                FilePath = "/tmp/sample.pdf",
                DataExtractionHook = hook
            };

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator, NullDataExtractionPreprocessor.Instance);
            var asset = Assert.IsType<DataExtractionAssetViewModel>(viewModel.SelectedAsset);

            asset.Caption = "Updated caption";

            await viewModel.SaveAsyncCommand.ExecuteAsync(null);

            var persisted = Assert.Single(item.DataExtractionHook!.Tables);
            Assert.Equal(table.FriendlyName, persisted.FriendlyName);
            Assert.Equal(table.Notes, persisted.Notes);
            Assert.Equal(table.ImagePath, persisted.ImagePath);
            Assert.Equal(table.ImageProvenanceHash, persisted.ImageProvenanceHash);
            Assert.Equal(table.LinkedEndpointIds, persisted.LinkedEndpointIds);
            Assert.Equal(table.LinkedInterventionIds, persisted.LinkedInterventionIds);
            Assert.Equal(table.PagePositions.Count, persisted.PagePositions.Count);

            var originalPosition = table.PagePositions[0];
            var persistedPosition = persisted.PagePositions[0];
            Assert.Equal(originalPosition.PageNumber, persistedPosition.PageNumber);
            Assert.Equal(originalPosition.Left, persistedPosition.Left);
            Assert.Equal(originalPosition.Top, persistedPosition.Top);
            Assert.Equal(originalPosition.Width, persistedPosition.Width);
            Assert.Equal(originalPosition.Height, persistedPosition.Height);
            Assert.Equal(originalPosition.PageWidth, persistedPosition.PageWidth);
            Assert.Equal(originalPosition.PageHeight, persistedPosition.PageHeight);
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
