using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Core.Abstractions;
using LM.Core.Models.DataExtraction;
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
            viewModel.StudyDetails.SiteCount = 5;
            viewModel.StudyDetails.TrialClassification = viewModel.StudyDetails.TrialClassificationOptions[1];
            viewModel.StudyDetails.IsRegistryStudy = true;
            viewModel.StudyDetails.IsCohortStudy = false;
            viewModel.StudyDetails.GeographyScope = viewModel.StudyDetails.GeographyScopeOptions[0];

            await viewModel.SaveAsyncCommand.ExecuteAsync(null);

            Assert.Single(item.DataExtractionHook!.Tables);
            Assert.Equal(3, item.DataExtractionHook.Tables[0].ColumnCountHint);
            Assert.Equal("baseline.dictionary", item.DataExtractionHook.Tables[0].DictionaryPath);
            Assert.Single(item.PendingChangeLogEvents);
            Assert.Equal(viewModel.StudyDetails.StudyDesign, item.DataExtractionHook.StudyDesign);
            Assert.Equal(5, item.DataExtractionHook.SiteCount);
            Assert.Equal(viewModel.StudyDetails.TrialClassification, item.DataExtractionHook.TrialClassification);
            Assert.True(item.DataExtractionHook.IsRegistryStudy.GetValueOrDefault());
            Assert.False(item.DataExtractionHook.IsCohortStudy.GetValueOrDefault());
            Assert.Equal(viewModel.StudyDetails.GeographyScope, item.DataExtractionHook.GeographyScope);

            var tags = item.PendingChangeLogEvents[0].Details?.Tags;
            Assert.NotNull(tags);
            Assert.Contains("RegistryStudy", tags!);
            Assert.DoesNotContain("CohortStudy", tags!);
            Assert.Contains("SiteCount:5", tags!);
            Assert.Contains(FormattableString.Invariant($"TrialClassification:{viewModel.StudyDetails.TrialClassification}"), tags!);
        }

        [Fact]
        public void LoadFromItem_Populates_StudyDetails_Metadata()
        {
            var hook = new LM.HubSpoke.Models.DataExtractionHook
            {
                StudyDesign = "Randomized controlled trial",
                StudySetting = "Multicenter",
                SiteCount = 12,
                TrialClassification = "Meta-analysis",
                IsRegistryStudy = true,
                IsCohortStudy = true,
                GeographyScope = "International"
            };

            var item = new StagingItem
            {
                FilePath = "/tmp/sample.pdf",
                DataExtractionHook = hook
            };

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator, NullDataExtractionPreprocessor.Instance);

            Assert.Equal(12, viewModel.StudyDetails.SiteCount);
            Assert.Equal("Meta-analysis", viewModel.StudyDetails.TrialClassification);
            Assert.True(viewModel.StudyDetails.IsRegistryStudy);
            Assert.True(viewModel.StudyDetails.IsCohortStudy);
            Assert.Equal("International", viewModel.StudyDetails.GeographyScope);
            Assert.Equal("Randomized controlled trial", viewModel.StudyDetails.StudyDesign);
            Assert.Equal("Multicenter", viewModel.StudyDetails.StudySetting);
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
        public async Task ExtractSelectedAssetCommand_Populates_Table_Metadata()
        {
            var table = new PreprocessedTable
            {
                Id = "table-01",
                Title = "Baseline characteristics",
                Classification = TableClassificationKind.Baseline,
                PageNumbers = new[] { 1 },
                CsvRelativePath = "staging/manual/tables/table-01.csv",
                ImageRelativePath = "staging/manual/tables/table-01.png",
                ProvenanceHash = "sha256-hash",
                ImageProvenanceHash = "img-hash",
                Tags = new List<string> { "baseline" },
                FriendlyName = "Baseline characteristics",
                Columns = new List<TableColumnMapping>
                {
                    new() { ColumnIndex = 0, Header = "Group", Role = TableColumnRole.Population },
                    new() { ColumnIndex = 1, Header = "Value", Role = TableColumnRole.Value }
                },
                Regions = new List<TableRegion>
                {
                    new() { PageNumber = 1, X = 0.1, Y = 0.2, Width = 0.3, Height = 0.4, Label = "Region 1" }
                },
                PageLocations = new List<TablePageLocation>
                {
                    new()
                    {
                        PageNumber = 1,
                        Left = 72,
                        Top = 144,
                        Width = 320,
                        Height = 200,
                        PageWidth = 612,
                        PageHeight = 792
                    }
                }
            };

            var preprocessResult = new DataExtractionPreprocessResult
            {
                Tables = new[] { table }
            };

            var preprocessor = new FakeDataExtractionPreprocessor(preprocessResult);

            var item = new StagingItem
            {
                FilePath = "/tmp/sample.pdf"
            };

            var viewModel = new DataExtractionWorkspaceViewModel(item, _orchestrator, preprocessor);

            viewModel.AddTableCommand.Execute(null);
            var asset = Assert.IsType<DataExtractionAssetViewModel>(viewModel.SelectedAsset);

            var draft = new PdfRegionDraft(1, 0.1, 0.2, 0.3, 0.4);
            viewModel.CreateRegionFromViewerCommand.Execute(draft);

            Assert.True(viewModel.ExtractSelectedAssetCommand.CanExecute(null));

            await viewModel.ExtractSelectedAssetCommand.ExecuteAsync(null);

            Assert.Equal("Baseline characteristics", asset.Title);
            Assert.Equal("Baseline", asset.Caption);
            Assert.Equal("staging/manual/tables/table-01.csv", asset.SourcePath);
            Assert.Equal("sha256-hash", asset.ProvenanceHash);
            Assert.Equal("staging/manual/tables/table-01.png", asset.TableImagePath);
            Assert.Equal("img-hash", asset.ImageProvenanceHash);
            Assert.Equal("baseline", asset.Tags);
            Assert.Equal(2, asset.ColumnHint);
            Assert.Equal("Baseline characteristics", asset.FriendlyName);
            Assert.Equal("1", asset.Pages);
            Assert.Single(asset.Regions);
            Assert.Equal("Region 1", asset.Regions[0].Label);
            Assert.Single(asset.PagePositions);
            var position = asset.PagePositions[0];
            Assert.Equal(1, position.PageNumber);
            Assert.Equal(72, position.Left);
            Assert.Equal(144, position.Top);
            Assert.Equal(320, position.Width);
            Assert.Equal(200, position.Height);
            Assert.Equal(612, position.PageWidth);
            Assert.Equal(792, position.PageHeight);
            Assert.Same(asset.Regions[0], viewModel.SelectedRegion);
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

        private sealed class FakeDataExtractionPreprocessor : IDataExtractionPreprocessor
        {
            private readonly DataExtractionPreprocessResult _result;

            public FakeDataExtractionPreprocessor(DataExtractionPreprocessResult result)
            {
                _result = result ?? throw new ArgumentNullException(nameof(result));
            }

            public Task<DataExtractionPreprocessResult> PreprocessAsync(DataExtractionPreprocessRequest request, CancellationToken ct = default)
            {
                return Task.FromResult(_result);
            }
        }
    }
}
