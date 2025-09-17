#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Infrastructure.Search
{
    public sealed class PubMedSearchProvider
    {
        private readonly HttpClient _http = new HttpClient();

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, DateTime? from, DateTime? to, CancellationToken ct)
        {
            // ESearch
            string term = query;
            if (from.HasValue || to.HasValue)
            {
                var f = (from ?? new DateTime(1800, 1, 1)).ToString("yyyy/MM/dd");
                var t = (to ?? DateTime.Today).ToString("yyyy/MM/dd");
                term = $"({query}) AND (\"{f}\"[Date - Publication] : \"{t}\"[Date - Publication])";
            }

            var esUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&retmode=json&retmax=200&term={Uri.EscapeDataString(term)}";
            using var es = await _http.GetAsync(esUrl, ct);
            es.EnsureSuccessStatusCode();
            using var esDoc = JsonDocument.Parse(await es.Content.ReadAsStreamAsync(ct));
            var ids = esDoc.RootElement.GetProperty("esearchresult").GetProperty("idlist")
                         .EnumerateArray().Select(x => x.GetString()!).ToArray();

            if (ids.Length == 0) return Array.Empty<SearchHit>();

            // ESummary (faster than EFetch for list view)
            var sumUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esummary.fcgi?db=pubmed&retmode=json&id={string.Join(",", ids)}";
            using var sum = await _http.GetAsync(sumUrl, ct);
            sum.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await sum.Content.ReadAsStreamAsync(ct));
            var root = doc.RootElement.GetProperty("result");

            var hits = new List<SearchHit>(ids.Length);
            foreach (var id in ids)
            {
                if (!root.TryGetProperty(id, out var it)) continue;
                string? title = it.TryGetProperty("title", out var t) ? t.GetString() : null;
                var year = TryYear(it.TryGetProperty("pubdate", out var pd) ? pd.GetString() : null);
                string authors = it.TryGetProperty("authors", out var a)
                    ? string.Join("; ", a.EnumerateArray().Select(n => n.TryGetProperty("name", out var n2) ? n2.GetString() : null).Where(s => !string.IsNullOrWhiteSpace(s)))
                    : "";
                string? journal = it.TryGetProperty("fulljournalname", out var j) ? j.GetString() : null;
                string? doi = ExtractDoi(it);

                hits.Add(new SearchHit
                {
                    Source = SearchDatabase.PubMed,
                    ExternalId = id,
                    Doi = doi,
                    Title = title ?? "",
                    Authors = authors,
                    JournalOrSource = journal,
                    Year = year,
                    Url = $"https://pubmed.ncbi.nlm.nih.gov/{id}/"
                });
            }
            return hits;

            static int? TryYear(string? s) => (s != null && s.Length >= 4 && int.TryParse(s[..4], out var y)) ? y : null;
            static string? ExtractDoi(JsonElement it)
            {
                if (!it.TryGetProperty("articleids", out var arr)) return null;
                foreach (var x in arr.EnumerateArray())
                {
                    if (x.TryGetProperty("idtype", out var tp) && string.Equals(tp.GetString(), "doi", StringComparison.OrdinalIgnoreCase))
                        return x.TryGetProperty("value", out var v) ? v.GetString() : null;
                }
                return null;
            }
        }
    }
}
