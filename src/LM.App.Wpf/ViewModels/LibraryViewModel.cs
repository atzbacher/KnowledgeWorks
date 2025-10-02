using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.Library.Search;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.ViewModels.Library.Collections;
using LM.App.Wpf.ViewModels.Library.LitSearch;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Core.Models.Search;
using LM.HubSpoke.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>
    /// Coordinates Library filters, search execution, and result presentation.
    /// </summary>
    public sealed partial class LibraryViewModel : ViewModelBase
    {
        private readonly IEntryStore _store;
        private readonly IFullTextSearchService _fullTextSearch;
        private readonly LibrarySearchParser _metadataParser = new();
        private readonly LibrarySearchEvaluator _metadataEvaluator = new();
        private readonly LitSearchTreeViewModel _litSearchOrganizer;
        private readonly LibraryCollectionsViewModel _collections;

        public LibraryViewModel(IEntryStore store,
                                IFullTextSearchService fullTextSearch,
                                LibraryFiltersViewModel filters,
                                LibraryResultsViewModel results,
                                IWorkSpaceService workspace,
                                IUserPreferencesStore preferencesStore,
                                IClipboardService clipboard,
                                IFileExplorerService fileExplorer,
                                ILibraryDocumentService documentService,
                                LitSearchTreeViewModel litSearchOrganizer,
                                LibraryCollectionsViewModel collections)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _fullTextSearch = fullTextSearch ?? throw new ArgumentNullException(nameof(fullTextSearch));
            Filters = filters ?? throw new ArgumentNullException(nameof(filters));
            Results = results ?? throw new ArgumentNullException(nameof(results));

            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _fileExplorer = fileExplorer ?? throw new ArgumentNullException(nameof(fileExplorer));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _litSearchOrganizer = litSearchOrganizer ?? throw new ArgumentNullException(nameof(litSearchOrganizer));
            _collections = collections ?? throw new ArgumentNullException(nameof(collections));

            Results.SelectionChanged += OnResultsSelectionChanged;

            _ = Filters.InitializeAsync();
            InitializeColumns();
            _ = LoadPreferencesAsync();
            _ = _litSearchOrganizer.RefreshAsync();
            _ = _collections.RefreshAsync();
        }

        public LibraryFiltersViewModel Filters { get; }
        public LibraryResultsViewModel Results { get; }

        public LitSearchTreeViewModel LitSearchOrganizer => _litSearchOrganizer;

        public LibraryCollectionsViewModel Collections => _collections;

        [RelayCommand]
        private async Task SearchAsync()
        {
            try
            {
                var directives = Filters.ApplyInlineSearchDirectives();
                Results.Clear();
                if (Filters.UseFullTextSearch)
                {
                    await RunFullTextSearchAsync().ConfigureAwait(false);
                }
                else
                {
                    await RunMetadataSearchAsync(directives.MetadataQuery).ConfigureAwait(false);
                }

                await Filters.RefreshNavigationAsync().ConfigureAwait(false);
                await _litSearchOrganizer.RefreshAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryViewModel] SearchAsync FAILED: {ex}");
                throw;
            }
        }

        private async Task RunMetadataSearchAsync(string metadataQuery)
        {
            Trace.WriteLine($"[LibraryViewModel] Executing metadata search with query '{metadataQuery}'.");
            var expression = _metadataParser.Parse(metadataQuery);
            var matches = new List<Entry>();
            var filteredOut = 0;

            await foreach (var entry in _store.EnumerateAsync())
            {
                if (entry is null)
                {
                    continue;
                }

                if (!_metadataEvaluator.Matches(entry, expression))
                {
                    continue;
                }

                if (!MatchesFilters(entry))
                {
                    filteredOut++;
                    continue;
                }

                matches.Add(entry);
            }

            Trace.WriteLine($"[LibraryViewModel] Metadata filters excluded {filteredOut} entries.");

            var sortOption = Filters.SelectedSort ?? LibrarySortOptions.NewestFirst;
            Results.ResetSortForSearch(sortOption);

            Debug.WriteLine($"[LibraryViewModel] Metadata search → {matches.Count} rows");

            Results.LoadMetadataResults(matches);

            if (matches.Count == 0)
                Debug.WriteLine("[LibraryViewModel] No entries matched metadata query");
        }

        private async Task RunFullTextSearchAsync()
        {
            var sortOption = Filters.SelectedSort ?? LibrarySortOptions.NewestFirst;
            Results.ResetSortForSearch(sortOption);

            var trimmed = Filters.GetNormalizedFullTextQuery();
            if (string.IsNullOrEmpty(trimmed))
            {
                Results.MarkAsMetadataResults();
                return;
            }

            var query = Filters.BuildFullTextQuery(trimmed);
            var hits = await _fullTextSearch.SearchAsync(query).ConfigureAwait(false);

            Debug.WriteLine($"[LibraryViewModel] Full-text search → {hits.Count} hits");

            var filteredOut = 0;
            await Results.LoadFullTextResultsAsync(hits, entry =>
            {
                var matchesFilters = MatchesFilters(entry);
                if (!matchesFilters)
                {
                    filteredOut++;
                }

                return matchesFilters;
            }).ConfigureAwait(false);

            Trace.WriteLine($"[LibraryViewModel] Full-text filters excluded {filteredOut} hits before rendering results.");

            if (hits.Count == 0)
                Debug.WriteLine("[LibraryViewModel] Full-text search returned no matches");
        }

        internal async Task HandleNavigationSelectionAsync(LibraryNavigationNodeViewModel? node)
        {
            if (node is null)
            {
                return;
            }

            switch (node.Kind)
            {
                case LibraryNavigationNodeKind.SavedSearch when node.Payload is LibrarySavedSearchPayload saved:
                    if (await Filters.ApplyPresetAsync(saved.Summary).ConfigureAwait(false))
                    {
                        await SearchAsync().ConfigureAwait(false);
                    }
                    break;

                case LibraryNavigationNodeKind.LitSearchRun when node.Payload is LibraryLitSearchRunPayload run:
                    await LoadLitSearchRunAsync(run).ConfigureAwait(false);
                    break;
            }
        }

        private async Task LoadLitSearchRunAsync(LibraryLitSearchRunPayload payload)
        {
            if (payload is null)
            {
                return;
            }

            Filters.UseFullTextSearch = false;

            var entries = await ReadCheckedEntriesAsync(payload).ConfigureAwait(false);
            if (entries.Count == 0)
            {
                Debug.WriteLine($"[LibraryViewModel] LitSearch run '{payload.RunId}' had no stored checked entries.");
                Results.Clear();
                return;
            }

            var sortOption = Filters.SelectedSort ?? LibrarySortOptions.NewestFirst;
            Results.ResetSortForSearch(sortOption);
            Results.LoadMetadataResults(entries);
        }

        private async Task<List<Entry>> ReadCheckedEntriesAsync(LibraryLitSearchRunPayload payload)
        {
            var results = new List<Entry>();

            if (string.IsNullOrWhiteSpace(payload.CheckedEntriesPath) || !File.Exists(payload.CheckedEntriesPath))
            {
                return results;
            }

            try
            {
                await using var stream = new FileStream(payload.CheckedEntriesPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                var sidecar = await JsonSerializer.DeserializeAsync<CheckedEntryIdsSidecar>(stream, JsonStd.Options).ConfigureAwait(false);
                var ids = sidecar?.CheckedEntries?.EntryIds;
                if (ids is null || ids.Count == 0)
                {
                    return results;
                }

                foreach (var id in ids)
                {
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var entry = await _store.GetByIdAsync(id).ConfigureAwait(false);
                    if (entry is not null)
                    {
                        results.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryViewModel] Failed to load litsearch run '{payload.RunId}': {ex}");
            }

            return results;
        }

        private bool MatchesFilters(Entry entry)
        {
            if (entry is null)
            {
                return false;
            }

            if (entry.IsBlacklisted)
            {
                Trace.WriteLine($"[LibraryViewModel] Entry {entry.Id} filtered out because it is blacklisted.");
                return false;
            }

            if (Filters.DateFrom is System.DateTime from && entry.AddedOnUtc.Date < from.Date)
            {
                Trace.WriteLine($"[LibraryViewModel] Entry {entry.Id} filtered out before {from:yyyy-MM-dd}.");
                return false;
            }

            if (Filters.DateTo is System.DateTime to && entry.AddedOnUtc.Date > to.Date)
            {
                Trace.WriteLine($"[LibraryViewModel] Entry {entry.Id} filtered out after {to:yyyy-MM-dd}.");
                return false;
            }

            if (Filters.SelectedTags.Count > 0)
            {
                if (entry.Tags is null || entry.Tags.Count == 0)
                {
                    Trace.WriteLine($"[LibraryViewModel] Entry {entry.Id} filtered out due to missing tags.");
                    return false;
                }

                var entryTags = entry.Tags
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(static tag => tag.Trim())
                    .ToList();

                foreach (var tag in Filters.SelectedTags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    var normalized = tag.Trim();
                    if (!entryTags.Any(entryTag => string.Equals(entryTag, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        Trace.WriteLine($"[LibraryViewModel] Entry {entry.Id} filtered out due to missing tag '{normalized}'.");
                        return false;
                    }
                }
            }

            return true;
        }

        private sealed record CheckedEntryIdsSidecar
        {
            [JsonPropertyName("checkedEntries")]
            public CheckedEntriesPayload CheckedEntries { get; init; } = new();
        }

        private sealed record CheckedEntriesPayload
        {
            [JsonPropertyName("entryIds")]
            public List<string> EntryIds { get; init; } = new();
        }

        // Add this property if not already present
        public LibraryCollectionsViewModel Collections { get; private set; }

        // Add this property to store all unique tags
        private ObservableCollection<string> _allTags = new();
        public ObservableCollection<string> AllTags => _allTags;

        // Method to load entries from a specific collection
        public async Task LoadCollectionEntriesAsync(string collectionId, CancellationToken ct = default)
        {
            try
            {
                Trace.WriteLine($"[LibraryViewModel] Loading entries for collection '{collectionId}'.");

                // Get the collection hierarchy to find entry IDs
                var hierarchy = await _collectionStore.GetHierarchyAsync(ct).ConfigureAwait(false);

                // Find the specific folder
                if (!hierarchy.TryFindFolder(collectionId, out var folder, out _) || folder is null)
                {
                    Trace.WriteLine($"[LibraryViewModel] Collection '{collectionId}' not found.");
                    return;
                }

                // Extract entry IDs from the collection
                var entryIds = folder.Entries.Select(e => e.EntryId).ToHashSet(StringComparer.Ordinal);

                if (entryIds.Count == 0)
                {
                    Trace.WriteLine("[LibraryViewModel] Collection is empty.");
                    await InvokeOnDispatcherAsync(() =>
                    {
                        Results.Items.Clear();
                        Results.TotalCount = 0;
                    }).ConfigureAwait(false);
                    return;
                }

                // Load all entries from the database
                var allEntries = await _store.ListAllAsync(ct).ConfigureAwait(false);

                // Filter to only entries in this collection
                var collectionEntries = allEntries
                    .Where(e => entryIds.Contains(e.InternalId))
                    .ToList();

                Trace.WriteLine($"[LibraryViewModel] Loaded {collectionEntries.Count} entries from collection.");

                // Update the results
                await InvokeOnDispatcherAsync(() =>
                {
                    Results.LoadMetadataResults(collectionEntries);
                    Results.TotalCount = collectionEntries.Count;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[LibraryViewModel] Failed to load collection entries: {ex.Message}");
            }
        }

        // Method to refresh all tags from the database
        public async Task RefreshTagsAsync(CancellationToken ct = default)
        {
            try
            {
                var allEntries = await _store.ListAllAsync(ct).ConfigureAwait(false);

                var uniqueTags = allEntries
                    .Where(e => e.Tags != null && e.Tags.Count > 0)
                    .SelectMany(e => e.Tags)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await InvokeOnDispatcherAsync(() =>
                {
                    _allTags.Clear();
                    foreach (var tag in uniqueTags)
                    {
                        _allTags.Add(tag);
                    }
                }).ConfigureAwait(false);

                Trace.WriteLine($"[LibraryViewModel] Refreshed {uniqueTags.Count} unique tags.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[LibraryViewModel] Failed to refresh tags: {ex.Message}");
            }
        }

        // Initialize method that should be called when the view loads
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await Filters.InitializeAsync(ct).ConfigureAwait(false);
            await Collections.RefreshAsync(ct).ConfigureAwait(false);
            await LitSearchOrganizer.RefreshAsync(ct).ConfigureAwait(false);
            await RefreshTagsAsync(ct).ConfigureAwait(false);
        }

        private static Task InvokeOnDispatcherAsync(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        // Add these private fields if not present:
        private readonly LibraryCollectionStore _collectionStore;
        private readonly IEntryStore _store;



    }
}
