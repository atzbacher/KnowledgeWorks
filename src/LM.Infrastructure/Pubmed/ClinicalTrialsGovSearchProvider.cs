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
                var protocol = s.GetProperty("protocolSection");
                var identification = protocol.GetProperty("identificationModule");
                var statusModule = protocol.GetProperty("statusModule");

                string? nct = identification.GetProperty("nctId").GetString();
                string? title = identification.GetProperty("briefTitle").GetString();
                string? status = statusModule.TryGetProperty("overallStatus", out var statusElement)
                    ? statusElement.GetString()
                    : null;

                var authorNames = new List<string>();
                if (protocol.TryGetProperty("contactsLocationsModule", out var contacts)
                    && contacts.TryGetProperty("overallOfficials", out var officials)
                    && officials.ValueKind == JsonValueKind.Array)
                {
                    foreach (var official in officials.EnumerateArray())
                    {
                        string? name = official.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        string? role = official.TryGetProperty("role", out var roleElement) ? roleElement.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(name))
                            authorNames.Add(string.IsNullOrWhiteSpace(role) ? name! : $"{name} ({role})");
                    }
                }

                if (authorNames.Count == 0
                    && protocol.TryGetProperty("sponsorsCollaboratorsModule", out var sponsors)
                    && sponsors.TryGetProperty("leadSponsor", out var lead)
                    && lead.TryGetProperty("name", out var leadName))
                {
                    var sponsorName = leadName.GetString();
                    if (!string.IsNullOrWhiteSpace(sponsorName))
                        authorNames.Add(sponsorName!);
                }

                int? year = null;
                if (statusModule.TryGetProperty("startDateStruct", out var sd))
                {
                    var d = sd.GetProperty("date").GetString();
                    if (d != null && d.Length >= 4 && int.TryParse(d[..4], out var y)) year = y;
                }

                var authors = authorNames.Count == 0 ? string.Empty : string.Join("; ", authorNames);
                var source = string.IsNullOrWhiteSpace(status) ? "ClinicalTrials.gov" : $"ClinicalTrials.gov ({status})";

                list.Add(new SearchHit
                {
                    Source = SearchDatabase.ClinicalTrialsGov,
                    ExternalId = nct ?? "",
                    Title = title ?? "",
                    Authors = authors,
                    JournalOrSource = source,
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
