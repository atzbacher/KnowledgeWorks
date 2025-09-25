using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>
    /// Coordinates Library filters, search execution, and result presentation.
    /// </summary>
    public sealed partial class LibraryViewModel : ViewModelBase
    {
        private readonly IEntryStore _store;
        private readonly IFullTextSearchService _fullTextSearch;

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
            var filter = Filters.BuildEntryFilter();

            Debug.WriteLine(
                $"Filter: Title='{filter.TitleContains}', Author='{filter.AuthorContains}', " +
                $"Tags=[{string.Join(",", filter.TagsAny ?? new List<string>())}], " +
                $"Types=[{string.Join(",", filter.TypesAny ?? Array.Empty<EntryType>())}], " +
                $"YearFrom={filter.YearFrom}, YearTo={filter.YearTo}, IsInternal={filter.IsInternal}");

            var rows = await _store.SearchAsync(filter).ConfigureAwait(false);

            Debug.WriteLine($"[LibraryViewModel] Metadata search → {rows.Count} rows");

            Results.LoadMetadataResults(rows);

            if (rows.Count == 0)
                Debug.WriteLine("[LibraryViewModel] No entries matched metadata filter");
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
    }
}
