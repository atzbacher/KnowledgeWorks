using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models.Search;

namespace LM.App.Wpf.ViewModels.Search
{
    /// <summary>
    /// Maintains the recent search execution history backed by <see cref="ISearchHistoryStore"/>.
    /// </summary>
    public sealed class SearchHistoryViewModel : ViewModelBase
    {
        private readonly ISearchHistoryStore _store;
        private readonly ObservableCollection<SearchHistoryEntry> _entries = new();
        private readonly ReadOnlyObservableCollection<SearchHistoryEntry> _recentEntries;
        private readonly TaskCompletionSource<bool> _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SearchHistoryViewModel(ISearchHistoryStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _recentEntries = new ReadOnlyObservableCollection<SearchHistoryEntry>(_entries);
        }

        public ReadOnlyObservableCollection<SearchHistoryEntry> RecentSearchHistory => _recentEntries;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                var document = await _store.LoadAsync(ct);
                _entries.Clear();
                if (document?.Entries is not null)
                {
                    foreach (var entry in document.Entries.OrderByDescending(e => e.ExecutedUtc))
                        _entries.Add(entry);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Trace.WriteLine($"[SearchHistoryViewModel] Failed to load history: {ex}");
            }
            finally
            {
                _initialized.TrySetResult(true);
            }
        }

        public async Task RecordExecutionAsync(SearchExecutionResult result, CancellationToken ct = default)
        {
            if (result is null)
                throw new ArgumentNullException(nameof(result));

            await _initialized.Task;

            if (string.IsNullOrWhiteSpace(result.Request.Query))
                return;

            var entry = new SearchHistoryEntry
            {
                Query = result.Request.Query.Trim(),
                Database = result.Request.Database,
                From = result.Request.From,
                To = result.Request.To,
                ExecutedUtc = result.ExecutedUtc
            };

            SearchHistoryEntry? existing = null;
            for (var i = 0; i < _entries.Count; i++)
            {
                var candidate = _entries[i];
                if (string.Equals(candidate.Query, entry.Query, StringComparison.OrdinalIgnoreCase) &&
                    candidate.Database == entry.Database &&
                    Nullable.Equals(candidate.From, entry.From) &&
                    Nullable.Equals(candidate.To, entry.To))
                {
                    existing = candidate;
                    break;
                }
            }

            if (existing is not null)
                _entries.Remove(existing);

            _entries.Insert(0, entry);

            const int maxHistory = 50;
            while (_entries.Count > maxHistory)
                _entries.RemoveAt(_entries.Count - 1);

            try
            {
                var document = new SearchHistoryDocument
                {
                    Entries = _entries.ToList()
                };
                await _store.SaveAsync(document, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Trace.WriteLine($"[SearchHistoryViewModel] Failed to persist history: {ex}");
            }
        }
    }
}
