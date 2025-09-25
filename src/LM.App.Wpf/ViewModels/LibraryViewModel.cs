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

            await foreach (var entry in _store.EnumerateAsync())
            {
                if (entry is null)
                {
                    continue;
                }

                if (_metadataEvaluator.Matches(entry, expression))
                {
                    matches.Add(entry);
                }
            }

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

            await Results.LoadFullTextResultsAsync(hits).ConfigureAwait(false);

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

        private static IReadOnlyList<Entry> SortEntries(IEnumerable<Entry> entries)
        {
            return entries
                .Where(static entry => entry is not null)
                .OrderByDescending(static entry => entry.Year.HasValue)
                .ThenByDescending(static entry => entry.Year ?? int.MinValue)
                .ThenBy(static entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.AddedOnUtc)
                .ThenBy(static entry => entry.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Take(1000)
                .ToList();
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
