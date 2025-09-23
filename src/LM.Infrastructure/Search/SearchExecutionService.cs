using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Search;
using LM.Core.Models;
using LM.Core.Models.Search;

namespace LM.Infrastructure.Search
{
    /// <summary>
    /// Executes external search providers and enriches their results with local metadata.
    /// </summary>
    public sealed class SearchExecutionService : ISearchExecutionService
    {
        private readonly IReadOnlyDictionary<SearchDatabase, ISearchProvider> _providers;
        private readonly IEntryStore _store;

        public SearchExecutionService(IEnumerable<ISearchProvider> providers, IEntryStore store)
        {
            if (providers is null)
                throw new ArgumentNullException(nameof(providers));

            _providers = providers.ToDictionary(p => p.Database);
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<SearchExecutionResult> ExecuteAsync(SearchExecutionRequest request, CancellationToken ct = default)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (!_providers.TryGetValue(request.Database, out var provider))
                throw new InvalidOperationException($"No provider registered for {request.Database}.");

            var hitsTask = provider.SearchAsync(request.Query, request.From, request.To, ct);
            var entriesTask = LoadEntriesAsync(ct);

            await Task.WhenAll(hitsTask, entriesTask).ConfigureAwait(false);

            var hits = hitsTask.Result;
            var entries = entriesTask.Result;

            foreach (var hit in hits)
            {
                var match = FindExistingEntry(entries, hit);
                hit.AlreadyInDb = match is not null;
                hit.ExistingEntryId = match?.Id;
            }

            return new SearchExecutionResult
            {
                Request = request,
                ExecutedUtc = DateTimeOffset.UtcNow,
                Hits = hits
            };
        }

        private async Task<List<Entry>> LoadEntriesAsync(CancellationToken ct)
        {
            var list = new List<Entry>();
            try
            {
                await foreach (var entry in _store.EnumerateAsync(ct).ConfigureAwait(false))
                    list.Add(entry);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Trace.WriteLine($"[SearchExecutionService] Failed to enumerate entries: {ex}");
            }

            return list;
        }

        private static Entry? FindExistingEntry(IReadOnlyList<Entry> entries, SearchHit hit)
        {
            if (entries.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(hit.Doi))
            {
                var doiMatch = entries.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Doi) &&
                    string.Equals(e.Doi, hit.Doi, StringComparison.OrdinalIgnoreCase));
                if (doiMatch is not null)
                    return doiMatch;
            }

            if (!string.IsNullOrWhiteSpace(hit.ExternalId))
            {
                var idMatch = entries.FirstOrDefault(e =>
                    (!string.IsNullOrWhiteSpace(e.Pmid) && string.Equals(e.Pmid, hit.ExternalId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(e.Nct) && string.Equals(e.Nct, hit.ExternalId, StringComparison.OrdinalIgnoreCase)));
                if (idMatch is not null)
                    return idMatch;
            }

            if (!string.IsNullOrWhiteSpace(hit.Title))
            {
                var titleMatch = entries.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Title) &&
                    string.Equals(e.Title, hit.Title, StringComparison.OrdinalIgnoreCase) &&
                    e.Year == hit.Year);
                if (titleMatch is not null)
                    return titleMatch;
            }

            return null;
        }
    }
}
