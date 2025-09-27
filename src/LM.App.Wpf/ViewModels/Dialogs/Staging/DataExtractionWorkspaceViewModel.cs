#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.DataExtraction;
using LM.Core.Utils;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed partial class DataExtractionWorkspaceViewModel : DialogViewModelBase
    {
        private readonly StagingItem _item;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IDataExtractionPreprocessor _preprocessor;
        private DataExtractionAssetViewModel? _selectedAsset;
        private DataExtractionRegionViewModel? _selectedRegion;
        private INotifyCollectionChanged? _trackedRegionCollection;
        private int _currentPage = 1;
        private int _pageCount;
        private double _zoom = 1d;
        private bool _isRegionCreationMode;

        public DataExtractionWorkspaceViewModel(StagingItem item,
                                                HookOrchestrator hookOrchestrator,
                                                IDataExtractionPreprocessor preprocessor)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));

            Assets = new ObservableCollection<DataExtractionAssetViewModel>();
            StudyDetails = new DataExtractionStudyDetailsViewModel();
            SaveAsyncCommand = new AsyncRelayCommand(SaveAsync);
            RedetectRegionsCommand = new AsyncRelayCommand(RedetectRegionsAsync, CanRedetectRegions);

            PdfPath = _item.FilePath;
            LoadFromItem();
        }

        public ObservableCollection<DataExtractionAssetViewModel> Assets { get; }

        public DataExtractionAssetViewModel? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                var previous = _selectedAsset;
                if (SetProperty(ref _selectedAsset, value))
                {
                    OnSelectedAssetChanged(previous, value);
                }
            }
        }

        public DataExtractionRegionViewModel? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                var previous = _selectedRegion;
                if (SetProperty(ref _selectedRegion, value))
                {
                    OnSelectedRegionChanged(previous, value);
                }
            }
        }

        public DataExtractionStudyDetailsViewModel StudyDetails { get; }

        public string PdfPath { get; }

        public string PdfDisplayName => string.IsNullOrWhiteSpace(PdfPath)
            ? string.Empty
            : Path.GetFileName(PdfPath);

        public bool HasPdf => !string.IsNullOrWhiteSpace(PdfPath) &&
                              string.Equals(Path.GetExtension(PdfPath), ".pdf", StringComparison.OrdinalIgnoreCase);

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                var normalized = Math.Max(1, value);
                if (PageCount > 0 && normalized > PageCount)
                {
                    normalized = PageCount;
                }

                if (SetProperty(ref _currentPage, normalized))
                {
                    RefreshNavigationCommands();
                }
            }
        }

        public int PageCount
        {
            get => _pageCount;
            set
            {
                var normalized = Math.Max(0, value);
                if (SetProperty(ref _pageCount, normalized))
                {
                    if (normalized > 0 && CurrentPage > normalized)
                    {
                        CurrentPage = normalized;
                    }
                    else
                    {
                        RefreshNavigationCommands();
                    }
                }
            }
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                var clamped = Math.Clamp(value, 0.5d, 4d);
                SetProperty(ref _zoom, clamped);
            }
        }

        public bool IsRegionCreationMode
        {
            get => _isRegionCreationMode;
            set => SetProperty(ref _isRegionCreationMode, value);
        }

        public IAsyncRelayCommand SaveAsyncCommand { get; }
        public IAsyncRelayCommand RedetectRegionsCommand { get; }

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
            {
                SelectedAsset = Assets.FirstOrDefault();
            }
        }

        private bool CanRemoveAsset(DataExtractionAssetViewModel? asset)
            => asset is not null && Assets.Contains(asset);

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void GoToPreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage -= 1;
            }
        }

        private bool CanGoToPreviousPage() => CurrentPage > 1;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void GoToNextPage()
        {
            if (PageCount == 0 || CurrentPage < PageCount)
            {
                CurrentPage += 1;
            }
        }

        private bool CanGoToNextPage() => PageCount == 0 || CurrentPage < PageCount;

        [RelayCommand]
        private void ZoomIn()
        {
            Zoom += 0.25d;
        }

        [RelayCommand]
        private void ZoomOut()
        {
            Zoom -= 0.25d;
        }

        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1d;
        }

        [RelayCommand]
        private void BeginRegionCreation()
        {
            IsRegionCreationMode = true;
        }

        [RelayCommand]
        private void CreateRegionFromViewer(PdfRegionDraft? draft)
        {
            if (draft is null || SelectedAsset is null)
                return;

            var clamped = ClampRegionDraft(draft);
            var region = SelectedAsset.CreateRegion(clamped.PageNumber, clamped.X, clamped.Y, clamped.Width, clamped.Height);
            SelectedRegion = region;
            EnsureCurrentPage(region.PageNumber);
            IsRegionCreationMode = false;
        }

        [RelayCommand]
        private void UpdateRegionFromViewer(PdfRegionUpdate? update)
        {
            if (update is null || update.Region is null || SelectedAsset is null)
                return;

            if (!SelectedAsset.Regions.Contains(update.Region))
                return;

            var clamped = ClampRegionDraft(new PdfRegionDraft(update.PageNumber, update.X, update.Y, update.Width, update.Height));
            update.Region.Apply(clamped.PageNumber, clamped.X, clamped.Y, clamped.Width, clamped.Height);
            SelectedRegion = update.Region;
            EnsureCurrentPage(update.Region.PageNumber);
        }

        private async Task RedetectRegionsAsync()
        {
            if (SelectedAsset is null)
                return;

            try
            {
                var request = new DataExtractionPreprocessRequest(PdfPath);
                var result = await _preprocessor.PreprocessAsync(request, CancellationToken.None).ConfigureAwait(true);

                var targetPages = ParsePages(SelectedAsset.Pages);
                var anchorPage = SelectedRegion?.PageNumber ?? (targetPages.Count > 0 ? targetPages[0] : 0);

                var match = result.Tables
                    .Select(table => new
                    {
                        Table = table,
                        Score = ScoreTableMatch(table, SelectedAsset, anchorPage, targetPages)
                    })
                    .OrderByDescending(t => t.Score)
                    .FirstOrDefault();

                if (match is null || match.Score <= 0)
                    return;

                SelectedAsset.Regions.Clear();
                foreach (var region in match.Table.Regions)
                {
                    SelectedAsset.CreateRegion(region.PageNumber, region.X, region.Y, region.Width, region.Height);
                }

                SelectedRegion = SelectedAsset.Regions.FirstOrDefault();
                if (SelectedRegion is not null)
                {
                    EnsureCurrentPage(SelectedRegion.PageNumber);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(FormattableString.Invariant($"[DataExtractionWorkspace] Region re-detection failed: {ex.Message}"));
            }
        }

        private bool CanRedetectRegions()
        {
            return SelectedAsset?.Kind == DataExtractionAssetKind.Table && HasPdf;
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }

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
                StudySetting = StudyDetails.StudySetting,
                SiteCount = StudyDetails.SiteCount,
                TrialClassification = StudyDetails.TrialClassification,
                IsRegistryStudy = StudyDetails.IsRegistryStudy,
                IsCohortStudy = StudyDetails.IsCohortStudy,
                GeographyScope = StudyDetails.GeographyScope
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
                    Tags = BuildMetadataTags(hook)
                }
            };
        }

        private static List<string> BuildMetadataTags(HookM.DataExtractionHook hook)
        {
            var tags = new List<string> { "DataExtraction", "Manual" };

            if (!string.IsNullOrWhiteSpace(hook.StudyDesign))
            {
                tags.Add(FormattableString.Invariant($"StudyDesign:{hook.StudyDesign}"));
            }

            if (!string.IsNullOrWhiteSpace(hook.StudySetting))
            {
                tags.Add(FormattableString.Invariant($"StudySetting:{hook.StudySetting}"));
            }

            if (!string.IsNullOrWhiteSpace(hook.TrialClassification))
            {
                tags.Add(FormattableString.Invariant($"TrialClassification:{hook.TrialClassification}"));
            }

            if (!string.IsNullOrWhiteSpace(hook.GeographyScope))
            {
                tags.Add(FormattableString.Invariant($"GeographyScope:{hook.GeographyScope}"));
            }

            if (hook.SiteCount is int siteCount && siteCount > 0)
            {
                tags.Add(FormattableString.Invariant($"SiteCount:{siteCount}"));
            }

            if (hook.IsRegistryStudy == true)
            {
                tags.Add("RegistryStudy");
            }

            if (hook.IsCohortStudy == true)
            {
                tags.Add("CohortStudy");
            }

            return tags;
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
                    Pages = string.Join(", ", preview.Pages),
                    FriendlyName = string.IsNullOrWhiteSpace(preview.Title) ? null : preview.Title,
                    LinkedEndpointIds = string.Join(", ", preview.Endpoints)
                };

                foreach (var region in preview.Regions)
                {
                    var regionVm = new DataExtractionRegionViewModel();
                    regionVm.Apply(region.PageNumber, region.X, region.Y, region.Width, region.Height);
                    regionVm.Label = region.Label ?? FormattableString.Invariant($"Table {table.Regions.Count + 1}");
                    table.Regions.Add(regionVm);
                }

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

        private void OnSelectedAssetChanged(DataExtractionAssetViewModel? previous, DataExtractionAssetViewModel? current)
        {
            DetachRegionCollection();
            IsRegionCreationMode = false;

            if (current is null)
            {
                SelectedRegion = null;
                CurrentPage = 1;
                RedetectRegionsCommand.NotifyCanExecuteChanged();
                return;
            }

            if (current.Regions is INotifyCollectionChanged notify)
            {
                _trackedRegionCollection = notify;
                notify.CollectionChanged += OnRegionsCollectionChanged;
            }

            SelectedRegion = current.Regions.FirstOrDefault();
            if (SelectedRegion is not null)
            {
                EnsureCurrentPage(SelectedRegion.PageNumber);
            }
            else
            {
                var page = GetPrimaryPageNumber(current);
                if (page > 0)
                {
                    EnsureCurrentPage(page);
                }
            }

            RedetectRegionsCommand.NotifyCanExecuteChanged();
        }

        private void OnSelectedRegionChanged(DataExtractionRegionViewModel? previous, DataExtractionRegionViewModel? current)
        {
            if (previous is not null)
            {
                previous.PropertyChanged -= OnSelectedRegionPropertyChanged;
            }

            if (current is not null)
            {
                current.PropertyChanged += OnSelectedRegionPropertyChanged;
                EnsureCurrentPage(current.PageNumber);
            }
        }

        private void OnSelectedRegionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(DataExtractionRegionViewModel.PageNumber), StringComparison.Ordinal))
                return;

            if (sender is DataExtractionRegionViewModel region && ReferenceEquals(region, SelectedRegion))
            {
                EnsureCurrentPage(region.PageNumber);
            }
        }

        private void OnRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, SelectedAsset?.Regions))
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SelectedRegion = SelectedAsset?.Regions.FirstOrDefault();
                return;
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DataExtractionRegionViewModel added)
                    {
                        SelectedRegion = added;
                    }
                }
            }

            if (SelectedRegion is not null && e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                {
                    if (ReferenceEquals(item, SelectedRegion))
                    {
                        SelectedRegion = SelectedAsset?.Regions.FirstOrDefault();
                        break;
                    }
                }
            }

            if (SelectedRegion is null)
            {
                SelectedRegion = SelectedAsset?.Regions.FirstOrDefault();
            }
        }

        private void DetachRegionCollection()
        {
            if (_trackedRegionCollection is not null)
            {
                _trackedRegionCollection.CollectionChanged -= OnRegionsCollectionChanged;
                _trackedRegionCollection = null;
            }
        }

        private void EnsureCurrentPage(int pageNumber)
        {
            if (pageNumber <= 0)
                return;

            if (PageCount > 0 && pageNumber > PageCount)
            {
                CurrentPage = PageCount;
                return;
            }

            CurrentPage = pageNumber;
        }

        private void RefreshNavigationCommands()
        {
            GoToNextPageCommand.NotifyCanExecuteChanged();
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
        }

        private static PdfRegionDraft ClampRegionDraft(PdfRegionDraft draft)
        {
            var width = Math.Clamp(draft.Width, 0.01d, 1d);
            var height = Math.Clamp(draft.Height, 0.01d, 1d);
            var x = Math.Clamp(draft.X, 0d, 1d - width);
            var y = Math.Clamp(draft.Y, 0d, 1d - height);
            var page = Math.Max(1, draft.PageNumber);
            return new PdfRegionDraft(page, x, y, width, height);
        }

        private static IReadOnlyList<int> ParsePages(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<int>();

            var results = new List<int>();
            var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0)
                {
                    results.Add(number);
                }
            }

            return results;
        }

        private static int GetPrimaryPageNumber(DataExtractionAssetViewModel asset)
        {
            if (asset.Regions.FirstOrDefault() is DataExtractionRegionViewModel region)
            {
                return region.PageNumber;
            }

            var pages = ParsePages(asset.Pages);
            return pages.FirstOrDefault();
        }

        private static int ScoreTableMatch(PreprocessedTable table,
                                           DataExtractionAssetViewModel asset,
                                           int anchorPage,
                                           IReadOnlyList<int> targetPages)
        {
            var score = 0;
            if (anchorPage > 0 && table.PageNumbers.Contains(anchorPage))
            {
                score += 5;
            }

            if (targetPages.Count > 0)
            {
                score += table.PageNumbers.Intersect(targetPages).Count();
            }

            if (!string.IsNullOrWhiteSpace(asset.Caption) &&
                string.Equals(asset.Caption, table.Classification.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }

            return score;
        }

        private static string GetCurrentUserName()
        {
            return SystemUser.GetCurrent();
        }
    }
}
