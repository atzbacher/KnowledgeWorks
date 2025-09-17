#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

// Alias the two model spaces so names never collide
using CoreM = LM.Core.Models;        // PublicationRecord, AbstractSection (Label + Text), etc.
using HookM = LM.HubSpoke.Models;    // ArticleHook, AbstractSection (Label + Content), etc.

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Builds a fully-populated LM.HubSpoke.Models.ArticleHook from a LM.Core.Models.PublicationRecord.
    /// Pure mapping; no I/O.
    /// </summary>
    public static class ArticleHookFactory
    {
        public static HookM.ArticleHook CreateFromPublication(CoreM.PublicationRecord r)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));

            // Prefer print date, then epub date
            HookM.PartialDate? pubDate = null;
            if (r.PublishedPrint is not null)
                pubDate = new HookM.PartialDate { Year = r.PublishedPrint.Value.Year, Month = r.PublishedPrint.Value.Month, Day = r.PublishedPrint.Value.Day };
            else if (r.PublishedEpub is not null)
                pubDate = new HookM.PartialDate { Year = r.PublishedEpub.Value.Year, Month = r.PublishedEpub.Value.Month, Day = r.PublishedEpub.Value.Day };

            // Pages split
            (string? startPage, string? endPage, string? articleNo) = SplitPages(r.Pages);

            // Build abstract FIRST, then assign inside the object initializer
            HookM.ArticleAbstract? abs = BuildAbstract(r);

            var hook = new HookM.ArticleHook
            {
                // Identifier
                Identifier = new HookM.ArticleIdentifier
                {
                    PMID = r.Pmid ?? string.Empty,
                    DOI = r.Doi,
                    PMCID = r.Pmcid,
                    OtherIds = new Dictionary<string, string>()
                },

                // Journal
                Journal = new HookM.JournalInfo
                {
                    Title = r.JournalTitle ?? string.Empty,
                    ISOAbbreviation = r.JournalIsoAbbrev,
                    Country = r.Country,
                    Issue = new HookM.JournalIssue
                    {
                        Volume = r.Volume,
                        Number = r.Issue,
                        PubDate = pubDate
                    }
                },

                // Article details
                Article = new HookM.ArticleDetails
                {
                    Title = r.Title ?? string.Empty,
                    Language = r.Language,
                    PublicationTypes = r.PublicationTypes?.ToList() ?? new List<string>(),
                    Pagination = new HookM.Pagination
                    {
                        StartPage = startPage,
                        EndPage = endPage,
                        ArticleNumber = articleNo
                    },
                    Dates = new HookM.ArticleDates
                    {
                        Electronic = r.PublishedEpub is null ? null
                                    : new DateTimeOffset(r.PublishedEpub.Value.Year, r.PublishedEpub.Value.Month, r.PublishedEpub.Value.Day, 0, 0, 0, TimeSpan.Zero),
                        Print = r.PublishedPrint is null ? null
                                    : new DateTimeOffset(r.PublishedPrint.Value.Year, r.PublishedPrint.Value.Month, r.PublishedPrint.Value.Day, 0, 0, 0, TimeSpan.Zero)
                    }
                },

                // ✅ Init-only property set in initializer (fixes CS8852)
                Abstract = abs
            };

            // Authors
            foreach (var a in r.Authors ?? Array.Empty<CoreM.AuthorName>())
            {
                hook.Authors.Add(new HookM.Author
                {
                    LastName = a.Family ?? a.LastFromLiteral(),
                    ForeName = a.Given,
                    ORCID = a.Orcid,
                    Affiliations = (a.Affiliations ?? Array.Empty<string>())
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Select(s => new HookM.Affiliation { Text = s })
                                   .ToList()
                });
            }

            // Keywords
            foreach (var k in r.Keywords ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(k)) hook.Keywords.Add(k.Trim());

            // MeSH: "Descriptor / Qualifier" → split
            foreach (var m in r.MeshHeadings ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(m)) continue;
                var parts = m.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var desc = parts.Length > 0 ? parts[0] : m;
                var quals = parts.Skip(1).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                hook.MeshHeadings.Add(new HookM.MeshHeading { Descriptor = desc, Qualifiers = quals, MajorTopic = false });
            }

            // Grants
            foreach (var g in r.Grants ?? Array.Empty<CoreM.GrantInfo>())
                hook.Grants.Add(new HookM.Grant { GrantId = g.GrantId, Agency = g.Agency, Country = g.Country });

            // References (PMIDs only)
            foreach (var pmid in r.ReferencedPmids ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(pmid)) hook.References.Add(new HookM.Citation { PMID = pmid });

            return hook;
        }

        private static HookM.ArticleAbstract? BuildAbstract(CoreM.PublicationRecord r)
        {
            var hasSections = (r.AbstractSections?.Count ?? 0) > 0;
            var hasPlain = !string.IsNullOrWhiteSpace(r.AbstractPlain);
            if (!hasSections && !hasPlain) return null;

            var sections = (r.AbstractSections ?? Array.Empty<CoreM.AbstractSection>())
                .Select(s => new HookM.AbstractSection
                {
                    Label = s.Label,
                    Text = s.Text ?? string.Empty
                })
                .ToList();

            return new HookM.ArticleAbstract
            {
                Sections = sections,
                Text = string.IsNullOrWhiteSpace(r.AbstractPlain) ? null : r.AbstractPlain
            };
        }

        private static (string? Start, string? End, string? ArticleNo) SplitPages(string? pages)
        {
            if (string.IsNullOrWhiteSpace(pages)) return (null, null, null);
            var p = pages.Trim();
            if (p.StartsWith("e", StringComparison.OrdinalIgnoreCase))
                return (null, null, p); // article number only

            var dash = p.IndexOf('-');
            if (dash > 0)
            {
                var s = p[..dash].Trim();
                var e = p[(dash + 1)..].Trim();
                return (s, e, null);
            }
            return (p, null, null);
        }
    }
}
