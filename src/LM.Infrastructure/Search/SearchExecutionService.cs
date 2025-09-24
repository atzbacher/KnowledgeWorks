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

            var hits = await provider.SearchAsync(request.Query, request.From, request.To, ct)
                .ConfigureAwait(false);

            foreach (var hit in hits)
            {
                var match = await TryMatchExistingEntryAsync(hit, ct).ConfigureAwait(false);
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

        private async Task<Entry?> TryMatchExistingEntryAsync(SearchHit hit, CancellationToken ct)
        {
            Entry? match = null;

            var doi = string.IsNullOrWhiteSpace(hit.Doi) ? null : hit.Doi;
            var pmid = GetPmidCandidate(hit);

            if (!string.IsNullOrWhiteSpace(doi) || !string.IsNullOrWhiteSpace(pmid))
            {
                try
                {
                    match = await _store.FindByIdsAsync(doi, pmid, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Trace.WriteLine($"[SearchExecutionService] FindByIdsAsync failed: {ex}");
                }
            }

            if (match is not null)
                return match;

            if (string.IsNullOrWhiteSpace(hit.Title))
                return null;

            try
            {
                var candidates = await _store.FindSimilarByNameYearAsync(hit.Title, hit.Year, ct)
                    .ConfigureAwait(false);

                return candidates.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Title) &&
                    string.Equals(e.Title, hit.Title, StringComparison.OrdinalIgnoreCase) &&
                    e.Year == hit.Year);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Trace.WriteLine($"[SearchExecutionService] FindSimilarByNameYearAsync failed: {ex}");
                return null;
            }
        }

        private static string? GetPmidCandidate(SearchHit hit)
        {
            if (string.IsNullOrWhiteSpace(hit.ExternalId))
                return null;

            if (hit.Source == SearchDatabase.PubMed)
                return hit.ExternalId;

            return hit.ExternalId.All(char.IsDigit) ? hit.ExternalId : null;
        }
    }
}
