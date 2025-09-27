using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.Core.Models;
using LM.Core.Models.DataExtraction;
using LM.Core.Utils;
using LM.HubSpoke.Models;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using Xunit;

namespace LM.App.Wpf.Tests.Dialogs.Staging
{
    public sealed class StagingTablesTabViewModelTests : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly WorkspaceService _workspace;
        private readonly HookOrchestrator _orchestrator;

        public StagingTablesTabViewModelTests()
        {
            _workspaceRoot = Path.Combine(Path.GetTempPath(), "kw_tests_" + Guid.NewGuid().ToString("N"));
            _workspace = new WorkspaceService();
            _workspace.EnsureWorkspaceAsync(_workspaceRoot).GetAwaiter().GetResult();
            _orchestrator = new HookOrchestrator(_workspace);
        }

        [Fact]
        public void Classification_Update_Persists_To_DataExtractionHook()
        {
            var dialog = new StubDialogService();
            var viewModel = new StagingTablesTabViewModel(_workspace, dialog, _orchestrator);
            var item = BuildItem();

            viewModel.Update(item);
            var row = Assert.Single(viewModel.Tables);

            row.Classification = TableClassificationKind.Baseline;

            Assert.Equal("Baseline", item.DataExtractionHook!.Tables[0].Caption);
        }

        [Fact]
        public async Task Reupload_Updates_SourcePath_And_ChangeLog()
        {
            var tempCsv = Path.Combine(_workspaceRoot, "source.csv");
            await File.WriteAllTextAsync(tempCsv, "col1,col2\n1,2\n");

            var dialog = new StubDialogService(tempCsv);
            var viewModel = new StagingTablesTabViewModel(_workspace, dialog, _orchestrator);
            var item = BuildItem();
            item.AttachToEntryId = null; // avoid writing changelog to disk

            viewModel.Update(item);
            var row = Assert.Single(viewModel.Tables);
            viewModel.SelectedTable = row;

            await viewModel.ReuploadDigitizedCommand.ExecuteAsync(row);

            var updatedTable = item.DataExtractionHook!.Tables[0];
            Assert.False(string.IsNullOrWhiteSpace(updatedTable.SourcePath));
            var absolute = _workspace.GetAbsolutePath(updatedTable.SourcePath!);
            Assert.True(File.Exists(absolute));
            Assert.StartsWith("sha256-", updatedTable.ProvenanceHash, StringComparison.OrdinalIgnoreCase);

            Assert.Single(item.PendingChangeLogEvents);
            var change = item.PendingChangeLogEvents[0];
            Assert.Equal("DigitizedCsvUpdated", change.Action);
            Assert.Equal(SystemUser.GetCurrent(), change.PerformedBy);
            Assert.Equal(item.DataExtractionHook.ExtractedBy, change.PerformedBy);
        }

        private static StagingItem BuildItem()
        {
            var preview = new StagingEvidencePreview
            {
                Tables = new List<StagingEvidencePreview.TablePreview>
                {
                    new StagingEvidencePreview.TablePreview
                    {
                        Title = "Table 1",
                        Classification = TableClassificationKind.Unknown,
                        Populations = new List<string> { "Adults" },
                        Endpoints = new List<string> { "Mortality" },
                        Pages = new List<int> { 1 }
                    }
                }
            };

            var extraction = new DataExtractionHook
            {
                Tables = new List<DataExtractionTable>
                {
                    new DataExtractionTable
                    {
                        Title = "Table 1",
                        Caption = TableClassificationKind.Unknown.ToString(),
                        SourcePath = string.Empty,
                        Pages = new List<string> { "1" },
                        LinkedEndpointIds = new List<string>(),
                        LinkedInterventionIds = new List<string>()
                    }
                }
            };

            return new StagingItem
            {
                FilePath = Path.Combine(Path.GetTempPath(), "alpha.pdf"),
                Title = "Sample",
                EvidencePreview = preview,
                DataExtractionHook = extraction
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_workspaceRoot))
                    Directory.Delete(_workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }

        private sealed class StubDialogService : IDialogService
        {
            private readonly string? _path;

            public StubDialogService(string? path = null)
            {
                _path = path;
            }

            public string[]? ShowOpenFileDialog(FilePickerOptions options)
                => _path is null ? Array.Empty<string>() : new[] { _path };

            public string? ShowFolderBrowserDialog(FolderPickerOptions options) => null;

            public string? ShowSaveFileDialog(FileSavePickerOptions options) => null;

            public bool? ShowStagingEditor(StagingListViewModel stagingList) => false;

            public bool? ShowDataExtractionWorkspace(StagingItem stagingItem) => null;
        }
    }
}
