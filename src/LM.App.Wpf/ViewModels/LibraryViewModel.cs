using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Core.Models.Search;
using LM.App.Wpf.Library.Search;
using LM.HubSpoke.Models;

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

        public LibraryViewModel(IEntryStore store,
                                IFullTextSearchService fullTextSearch,
                                LibraryFiltersViewModel filters,
                                LibraryResultsViewModel results,
                                IWorkSpaceService workspace,
                                IUserPreferencesStore preferencesStore,
                                IClipboardService clipboard,
                                IFileExplorerService fileExplorer,
                                ILibraryDocumentService documentService)
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

            Results.SelectionChanged += OnResultsSelectionChanged;

            _ = Filters.InitializeAsync();
            InitializeColumns();
            _ = LoadPreferencesAsync();
        }

        public LibraryFiltersViewModel Filters { get; }
        public LibraryResultsViewModel Results { get; }

        [RelayCommand]
        private async Task SearchAsync()
        {
            try
            {
                Results.Clear();
                if (Filters.UseFullTextSearch)
                    await RunFullTextSearchAsync().ConfigureAwait(false);
                else
                    await RunMetadataSearchAsync().ConfigureAwait(false);

                await Filters.RefreshNavigationAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryViewModel] SearchAsync FAILED: {ex}");
                throw;
            }
        }

        private async Task RunMetadataSearchAsync()
        {
            var expression = _metadataParser.Parse(Filters.UnifiedQuery);
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

            Trace.WriteLine($"[LibraryViewModel] Metadata filters excluded {filteredOut} entries before sorting.");

            var ordered = SortEntries(matches);

            Debug.WriteLine($"[LibraryViewModel] Metadata search → {ordered.Count} rows");

            Results.LoadMetadataResults(ordered);

            if (ordered.Count == 0)
                Debug.WriteLine("[LibraryViewModel] No entries matched metadata query");
        }

        private async Task RunFullTextSearchAsync()
        {
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

            var ordered = SortEntries(entries);
            Results.LoadMetadataResults(ordered);
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

        private IReadOnlyList<Entry> SortEntries(IEnumerable<Entry> entries)
        {
            var validEntries = entries
                .Where(static entry => entry is not null)
                .ToList();

            var sort = Filters.SelectedSort ?? LibrarySortOptions.NewestFirst;
            Trace.WriteLine($"[LibraryViewModel] Sorting {validEntries.Count} entries using option '{sort.Key}'.");

            var ordered = sort.Key switch
            {
                var key when string.Equals(key, LibrarySortOptions.OldestFirst.Key, StringComparison.OrdinalIgnoreCase)
                    => validEntries
                        .OrderBy(entry => entry.Year.HasValue ? 0 : 1)
                        .ThenBy(entry => entry.Year ?? int.MaxValue)
                        .ThenBy(entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.AddedOnUtc)
                        .ThenBy(entry => entry.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                var key when string.Equals(key, LibrarySortOptions.TitleAscending.Key, StringComparison.OrdinalIgnoreCase)
                    => validEntries
                        .OrderBy(entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(entry => entry.Year.HasValue)
                        .ThenByDescending(entry => entry.Year ?? int.MinValue)
                        .ThenBy(entry => entry.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.AddedOnUtc)
                        .ThenBy(entry => entry.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                var key when string.Equals(key, LibrarySortOptions.TitleDescending.Key, StringComparison.OrdinalIgnoreCase)
                    => validEntries
                        .OrderByDescending(entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(entry => entry.Year.HasValue)
                        .ThenByDescending(entry => entry.Year ?? int.MinValue)
                        .ThenBy(entry => entry.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.AddedOnUtc)
                        .ThenBy(entry => entry.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                _ => validEntries
                        .OrderByDescending(entry => entry.Year.HasValue)
                        .ThenByDescending(entry => entry.Year ?? int.MinValue)
                        .ThenBy(entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.AddedOnUtc)
                        .ThenBy(entry => entry.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            };

            return ordered.Take(1000).ToList();
        }

        private bool MatchesFilters(Entry entry)
        {
            if (entry is null)
            {
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
                    if (!entryTags.Any(entryTag => string.Equals(entryTag, normalized, StringComparer.OrdinalIgnoreCase)))
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
    }
}
