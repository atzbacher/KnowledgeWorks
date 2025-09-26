#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.Core.Models;
using LM.Core.Utils;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed partial class DataExtractionWorkspaceViewModel : DialogViewModelBase
    {
        private readonly StagingItem _item;
        private readonly HookOrchestrator _hookOrchestrator;
        private DataExtractionAssetViewModel? _selectedAsset;

        public DataExtractionWorkspaceViewModel(StagingItem item,
                                                HookOrchestrator hookOrchestrator)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));

            Assets = new ObservableCollection<DataExtractionAssetViewModel>();
            StudyDetails = new DataExtractionStudyDetailsViewModel();

            PdfPath = _item.FilePath;
            LoadFromItem();
        }

        public ObservableCollection<DataExtractionAssetViewModel> Assets { get; }

        public DataExtractionAssetViewModel? SelectedAsset
        {
            get => _selectedAsset;
            set => SetProperty(ref _selectedAsset, value);
        }

        public DataExtractionStudyDetailsViewModel StudyDetails { get; }

        public string PdfPath { get; }

        public string PdfDisplayName => string.IsNullOrWhiteSpace(PdfPath)
            ? ""
            : Path.GetFileName(PdfPath);

        public bool HasPdf => !string.IsNullOrWhiteSpace(PdfPath) &&
                              string.Equals(Path.GetExtension(PdfPath), ".pdf", StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private void AddTable()
        {
            var title = FormattableString.Invariant($"Table {Assets.Count(a => a.Kind == DataExtractionAssetKind.Table) + 1}");
            var table = new DataExtractionAssetViewModel(DataExtractionAssetKind.Table, Guid.NewGuid().ToString("N"), title)
            {
                Caption = "Manual selection",
                Pages = SuggestPages()
            };

            Assets.Add(table);
            SelectedAsset = table;
        }

        [RelayCommand]
        private void AddFigure()
        {
            var title = FormattableString.Invariant($"Figure {Assets.Count(a => a.Kind == DataExtractionAssetKind.Figure) + 1}");
            var figure = new DataExtractionAssetViewModel(DataExtractionAssetKind.Figure, Guid.NewGuid().ToString("N"), title)
            {
                Caption = "Manual selection",
                Pages = SuggestPages()
            };

            Assets.Add(figure);
            SelectedAsset = figure;
        }

        [RelayCommand(CanExecute = nameof(CanRemoveAsset))]
        private void RemoveAsset(DataExtractionAssetViewModel? asset)
        {
            if (asset is null)
                return;

            Assets.Remove(asset);
            if (ReferenceEquals(SelectedAsset, asset))
                SelectedAsset = Assets.FirstOrDefault();
        }

        private bool CanRemoveAsset(DataExtractionAssetViewModel? asset)
            => asset is not null && Assets.Contains(asset);

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var hook = _item.DataExtractionHook ?? new HookM.DataExtractionHook
            {
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = GetCurrentUserName()
            };

            var tables = Assets
                .Where(static asset => asset.Kind == DataExtractionAssetKind.Table)
                .Select(static asset => asset.ToTableHook())
                .ToList();

            var figures = Assets
                .Where(static asset => asset.Kind == DataExtractionAssetKind.Figure)
                .Select(static asset => asset.ToFigureHook())
                .ToList();

            var updated = new HookM.DataExtractionHook
            {
                SchemaVersion = hook.SchemaVersion,
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = GetCurrentUserName(),
                Populations = hook.Populations,
                Interventions = hook.Interventions,
                Endpoints = hook.Endpoints,
                Figures = figures,
                Tables = tables,
                Notes = hook.Notes,
                StudyDesign = StudyDetails.StudyDesign,
                StudySetting = StudyDetails.StudySetting
            };

            _item.DataExtractionHook = updated;

            var changeEvent = BuildChangeLogEvent(updated);
            _item.PendingChangeLogEvents.Add(changeEvent);

            if (!string.IsNullOrWhiteSpace(_item.AttachToEntryId))
            {
                var ctx = new HookContext
                {
                    ChangeLog = new HookM.EntryChangeLogHook
                    {
                        Events = new List<HookM.EntryChangeLogEvent> { changeEvent }
                    }
                };

                await _hookOrchestrator.ProcessAsync(_item.AttachToEntryId!, ctx, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            RequestClose(true);
        }

        private HookM.EntryChangeLogEvent BuildChangeLogEvent(HookM.DataExtractionHook hook)
        {
            var primaryFigure = hook.Figures.FirstOrDefault();
            var primaryTable = hook.Tables.FirstOrDefault();
            var attachmentId = primaryFigure?.Id ?? primaryTable?.Id ?? Guid.NewGuid().ToString("N");
            var libraryPath = primaryFigure?.SourcePath ?? primaryTable?.SourcePath ?? string.Empty;
            var title = _item.Title ?? _item.DisplayName ?? "Data extraction updated";

            return new HookM.EntryChangeLogEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow,
                PerformedBy = GetCurrentUserName(),
                Action = "ManualDataExtractionEdited",
                Details = new HookM.ChangeLogAttachmentDetails
                {
                    AttachmentId = attachmentId,
                    Title = title,
                    LibraryPath = libraryPath,
                    Purpose = AttachmentKind.Supplement,
                    Tags = new List<string> { "DataExtraction", "Manual" }
                }
            };
        }

        private void LoadFromItem()
        {
            var hook = _item.DataExtractionHook;
            if (hook is null)
            {
                hook = new HookM.DataExtractionHook
                {
                    ExtractedAtUtc = DateTime.UtcNow,
                    ExtractedBy = GetCurrentUserName()
                };
                _item.DataExtractionHook = hook;
            }

            StudyDetails.Load(hook);

            foreach (var table in hook.Tables)
            {
                Assets.Add(DataExtractionAssetViewModel.FromTable(table));
            }

            foreach (var figure in hook.Figures)
            {
                Assets.Add(DataExtractionAssetViewModel.FromFigure(figure));
            }

            if (Assets.Count == 0)
            {
                SeedFromPreview();
            }

            SelectedAsset = Assets.FirstOrDefault();
        }

        private void SeedFromPreview()
        {
            var tables = _item.EvidencePreview?.Tables ?? Array.Empty<StagingEvidencePreview.TablePreview>();
            foreach (var preview in tables)
            {
                var table = new DataExtractionAssetViewModel(DataExtractionAssetKind.Table, Guid.NewGuid().ToString("N"), preview.Title)
                {
                    Caption = preview.Classification.ToString(),
                    Pages = string.Join(", ", preview.Pages)
                };

                Assets.Add(table);
            }

            var figures = _item.EvidencePreview?.Figures ?? Array.Empty<StagingEvidencePreview.FigurePreview>();
            foreach (var preview in figures)
            {
                var figure = new DataExtractionAssetViewModel(DataExtractionAssetKind.Figure, Guid.NewGuid().ToString("N"), preview.Caption)
                {
                    Caption = preview.Caption,
                    Pages = string.Join(", ", preview.Pages),
                    ThumbnailPath = preview.ThumbnailPath,
                    ImagePath = preview.ThumbnailPath
                };

                Assets.Add(figure);
            }
        }

        private string SuggestPages()
        {
            var current = SelectedAsset?.Pages;
            if (!string.IsNullOrWhiteSpace(current))
                return current;

            var previewPages = _item.EvidencePreview?.Sections.SelectMany(static s => s.Pages).Distinct().ToList();
            if (previewPages is { Count: > 0 })
                return string.Join(", ", previewPages);

            return string.Empty;
        }

        private static string GetCurrentUserName()
        {
            return SystemUser.GetCurrent();
        }
    }
}
