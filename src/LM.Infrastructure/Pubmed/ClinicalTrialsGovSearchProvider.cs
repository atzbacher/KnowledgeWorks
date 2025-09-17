#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Infrastructure.Pubmed
{
    public sealed class ClinicalTrialsGovSearchProvider
    {
        private readonly HttpClient _http = new HttpClient();

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, DateTime? from, DateTime? to, CancellationToken ct)
        {
            // v2 API - study list (we’ll keep it small and page size=200)
            var url = $"https://clinicaltrials.gov/api/v2/studies?query.term={Uri.EscapeDataString(query)}&pageSize=200";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync(ct));
            var list = new List<SearchHit>();
            foreach (var s in doc.RootElement.GetProperty("studies").EnumerateArray())
            {
                string? nct = s.GetProperty("protocolSection").GetProperty("identificationModule").GetProperty("nctId").GetString();
                string? title = s.GetProperty("protocolSection").GetProperty("identificationModule").GetProperty("briefTitle").GetString();
                string? status = s.GetProperty("protocolSection").GetProperty("statusModule").GetProperty("overallStatus").GetString();
                int? year = null;
                if (s.GetProperty("protocolSection").GetProperty("statusModule").TryGetProperty("startDateStruct", out var sd))
                {
                    var d = sd.GetProperty("date").GetString();
                    if (d != null && d.Length >= 4 && int.TryParse(d[..4], out var y)) year = y;
                }

                list.Add(new SearchHit
                {
                    Source = SearchDatabase.ClinicalTrialsGov,
                    ExternalId = nct ?? "",
                    Title = title ?? "",
                    Authors = status ?? "",
                    JournalOrSource = "ClinicalTrials.gov",
                    Year = year,
                    Url = nct != null ? $"https://clinicaltrials.gov/study/{nct}" : null
                });
            }

            // Optional: post-filter by year range
            if (from.HasValue || to.HasValue)
            {
                int fromY = from?.Year ?? 0;
                int toY = to?.Year ?? 9999;
                list = list.Where(h => !h.Year.HasValue || (h.Year.Value >= fromY && h.Year.Value <= toY)).ToList();
            }

            return list;
        }
    }
}
