#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.DataExtraction;
using LM.Core.Utils;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed partial class StagingTablesTabViewModel : StagingTabViewModel
    {
        private readonly IWorkSpaceService _workspace;
        private readonly IDialogService _dialogs;
        private readonly HookOrchestrator _hookOrchestrator;
        private StagingTableRowViewModel? _selectedTable;

        public StagingTablesTabViewModel(IWorkSpaceService workspace,
                                         IDialogService dialogs,
                                         HookOrchestrator hookOrchestrator)
            : base("Tables")
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
        }

        public ObservableCollection<StagingTableRowViewModel> Tables { get; } = new();

        public IReadOnlyList<TableClassificationKind> ClassificationOptions { get; } = Enum.GetValues<TableClassificationKind>();

        public StagingTableRowViewModel? SelectedTable
        {
            get => _selectedTable;
            set => SetProperty(ref _selectedTable, value);
        }

        protected override void OnItemUpdated(StagingItem? item)
        {
            Tables.Clear();
            SelectedTable = null;

            if (item is null)
                return;

            item.DataExtractionHook ??= CreateEmptyExtractionHook();

            var previewTables = item.EvidencePreview?.Tables ?? Array.Empty<StagingEvidencePreview.TablePreview>();
            var hookTables = item.DataExtractionHook.Tables;

            for (var i = 0; i < Math.Max(previewTables.Count, hookTables.Count); i++)
            {
                if (i >= hookTables.Count)
                {
                    var fallback = previewTables.ElementAtOrDefault(i);
                    hookTables.Add(CreateTableFromPreview(fallback));
                }

                var hookTable = hookTables[i];
                var preview = previewTables.ElementAtOrDefault(i);
                var row = new StagingTableRowViewModel(hookTable, preview, OnClassificationChanged);
                Tables.Add(row);
            }

            SelectedTable = Tables.FirstOrDefault();
        }

        protected override void RefreshValidation()
        {
            if (Item is null)
            {
                SetValidationMessages(new[] { "Select a staged item to review tables." });
                return;
            }

            var issues = new List<string>();
            if (Tables.Count == 0)
            {
                issues.Add("No tables were detected for this staged evidence.");
            }
            else if (Tables.Any(static t => t.Classification == TableClassificationKind.Unknown))
            {
                issues.Add("Classify each table before committing evidence.");
            }

            SetValidationMessages(issues);
        }

        private void OnClassificationChanged(StagingTableRowViewModel row)
        {
            if (Item?.DataExtractionHook is null)
                return;

            var list = Item.DataExtractionHook.Tables;
            var index = list.FindIndex(t => t.Id == row.Id);
            if (index < 0)
                return;

            var existing = list[index];
            var updated = CloneTable(existing, caption: row.Classification.ToString());
            list[index] = updated;
            row.UpdateHook(updated);
            RefreshValidation();
        }

        [RelayCommand]
        private void LaunchDigitizer(StagingTableRowViewModel? row)
        {
            if (row is null)
                return;

            var path = ResolveAbsolutePath(row.SourcePath, row.Id);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(info);
            }
            catch
            {
                // Swallow: launching external tooling should not crash the dialog.
            }
        }

        [RelayCommand]
        private async Task ReuploadDigitizedAsync(StagingTableRowViewModel? row)
        {
            if (row is null || Item is null)
                return;

            var pick = _dialogs.ShowOpenFileDialog(new FilePickerOptions
            {
                AllowMultiple = false,
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            });

            var selected = pick?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected))
                return;

            var absoluteTarget = ResolveAbsolutePath(row.SourcePath, row.Id);
            if (string.IsNullOrWhiteSpace(absoluteTarget))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget)!);
            File.Copy(selected, absoluteTarget, overwrite: true);

            var relative = NormalizeRelativePath(absoluteTarget);
            var provenance = ComputeSha256Provenance(absoluteTarget);

            var hook = Item.DataExtractionHook ?? CreateEmptyExtractionHook();
            Item.DataExtractionHook = hook;

            var tables = hook.Tables;
            var index = tables.FindIndex(t => t.Id == row.Id);
            if (index < 0)
                return;

            var updated = CloneTable(tables[index], sourcePath: relative, provenanceHash: provenance, caption: row.Classification.ToString());
            tables[index] = updated;
            row.UpdateHook(updated);

            TouchExtractionMetadata(Item);
            await AppendReuploadChangeLogAsync(Item, updated, CancellationToken.None).ConfigureAwait(false);

            RefreshValidation();
        }

        private async Task AppendReuploadChangeLogAsync(StagingItem item, HookM.DataExtractionTable table, CancellationToken ct)
        {
            var evt = new HookM.EntryChangeLogEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                PerformedBy = GetCurrentUserName(),
                Action = "DigitizedCsvUpdated",
                Details = new HookM.ChangeLogAttachmentDetails
                {
                    AttachmentId = table.Id,
                    Title = table.Title,
                    LibraryPath = table.SourcePath ?? string.Empty,
                    Purpose = AttachmentKind.Supplement,
                    Tags = new List<string>()
                }
            };

            item.PendingChangeLogEvents.Add(evt);

            if (!string.IsNullOrWhiteSpace(item.AttachToEntryId))
            {
                var ctx = new HookContext
                {
                    ChangeLog = new HookM.EntryChangeLogHook
                    {
                        Events = new List<HookM.EntryChangeLogEvent> { evt }
                    }
                };

                await _hookOrchestrator.ProcessAsync(item.AttachToEntryId!, ctx, ct).ConfigureAwait(false);
            }
        }

        private static HookM.DataExtractionHook CreateEmptyExtractionHook()
            => new()
            {
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = GetCurrentUserName()
            };

        private static HookM.DataExtractionTable CreateTableFromPreview(StagingEvidencePreview.TablePreview? preview)
        {
            var title = preview?.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = "Table";

            return new HookM.DataExtractionTable
            {
                Title = title!,
                Caption = preview?.Classification.ToString(),
                SourcePath = string.Empty,
                ProvenanceHash = string.Empty,
                Pages = preview?.Pages.Select(static p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList() ?? new List<string>()
            };
        }

        private static HookM.DataExtractionTable CloneTable(HookM.DataExtractionTable source,
                                                            string? caption = null,
                                                            string? sourcePath = null,
                                                            string? provenanceHash = null)
        {
            return new HookM.DataExtractionTable
            {
                Id = source.Id,
                Title = source.Title,
                Caption = caption ?? source.Caption,
                SourcePath = sourcePath ?? source.SourcePath,
                Pages = new List<string>(source.Pages),
                LinkedEndpointIds = new List<string>(source.LinkedEndpointIds),
                LinkedInterventionIds = new List<string>(source.LinkedInterventionIds),
                ProvenanceHash = provenanceHash ?? source.ProvenanceHash,
                Notes = source.Notes,
                TableLabel = source.TableLabel,
                Summary = source.Summary
            };
        }

        private string ResolveAbsolutePath(string sourcePath, string tableId)
        {
            var normalized = sourcePath;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = Path.Combine("staging", "manual", "tables", tableId + ".csv").Replace(Path.DirectorySeparatorChar, '/');
            }

            return _workspace.GetAbsolutePath(normalized);
        }

        private string NormalizeRelativePath(string absolute)
        {
            var root = _workspace.GetWorkspaceRoot();
            var relative = Path.GetRelativePath(root, absolute);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ComputeSha256Provenance(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"sha256-{hex}";
        }

        private static void TouchExtractionMetadata(StagingItem item)
        {
            var hook = item.DataExtractionHook;
            if (hook is null)
                return;

            item.DataExtractionHook = new HookM.DataExtractionHook
            {
                SchemaVersion = hook.SchemaVersion,
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = GetCurrentUserName(),
                Populations = hook.Populations,
                Interventions = hook.Interventions,
                Endpoints = hook.Endpoints,
                Figures = hook.Figures,
                Tables = hook.Tables,
                Notes = hook.Notes,
                StudyDesign = hook.StudyDesign,
                StudySetting = hook.StudySetting
            };
        }

        private static string GetCurrentUserName()
        {
            return SystemUser.GetCurrent();
        }
    }
}
