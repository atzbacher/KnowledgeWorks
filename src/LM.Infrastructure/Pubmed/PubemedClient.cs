#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using LM.Core.Abstractions;   // IPublicationLookup
using LM.Core.Models;        // PublicationRecord, AuthorName, AbstractSection, GrantInfo

namespace LM.Infrastructure.PubMed
{
    public sealed class PubMedClient : IPublicationLookup
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

        /// <summary>
        /// Resolve DOI -> PMID (esearch), fetch article (efetch), map to PublicationRecord.
        /// Optionally call ELink to populate CitedBy (off by default; enable for detail pages).
        /// </summary>
        public async Task<PublicationRecord?> TryGetByDoiAsync(string doi, bool includeCitedBy, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(doi)) return null;

            // 1) esearch DOI -> PMID
            var term = Uri.EscapeDataString($"{doi}[DOI]");
            var esearchUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&retmode=json&term={term}";

            string? pmid;
            using (var resp = await _http.GetAsync(esearchUrl, ct))
            {
                if (!resp.IsSuccessStatusCode) return null;
                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("esearchresult", out var esr)) return null;
                if (!esr.TryGetProperty("idlist", out var idlist) || idlist.GetArrayLength() == 0) return null;
                pmid = idlist[0].GetString();
            }
            if (string.IsNullOrWhiteSpace(pmid)) return null;

            // 2) efetch PMID -> XML article
            var efetchUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&retmode=xml&id={pmid}";
            XDocument xml;
            using (var resp = await _http.GetAsync(efetchUrl, ct))
            {
                if (!resp.IsSuccessStatusCode) return null;
                xml = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            }

            var record = MapEfetch(xml, pmid);
            if (record is null) return null;

            // 3) optionally ELink for cited-by
            if (includeCitedBy)
            {
                var (count, cited) = await TryGetCitedByAsync(pmid, ct);
                record = WithCitedBy(record, count, cited);
            }

            return record;
        }

        internal static PublicationRecord? MapEfetch(XDocument x, string pmid)
        {
            // Works with PubmedArticleSet and direct PubmedArticle
            var article = x.Descendants("Article").FirstOrDefault();
            if (article is null) return null;

            // Title
            string? title = article.Element("ArticleTitle")?.Value?.Trim();

            // Journal block
            var journal = article.Element("Journal");
            string? jTitle = journal?.Element("Title")?.Value?.Trim();
            string? jIso = journal?.Element("ISOAbbreviation")?.Value?.Trim();
            string? volume = journal?.Element("JournalIssue")?.Element("Volume")?.Value?.Trim();
            string? issue = journal?.Element("JournalIssue")?.Element("Issue")?.Value?.Trim();
            string? pages = article.Element("Pagination")?.Element("MedlinePgn")?.Value?.Trim();

            // Dates / year
            int? year = TryYear(article);
            var (printDate, epubDate) = TryDates(article);

            // IDs
            var ids = x.Descendants("ArticleIdList").Elements("ArticleId").ToList();
            string? doi = ids.FirstOrDefault(e => (string?)e.Attribute("IdType") == "doi")?.Value?.Trim();
            string? pmcid = ids.FirstOrDefault(e => (string?)e.Attribute("IdType") == "pmc")?.Value?.Trim();

            // Authors (+ affiliations)
            var authors = new List<AuthorName>();
            var alist = article.Element("AuthorList");
            if (alist is not null)
            {
                foreach (var a in alist.Elements("Author"))
                {
                    var coll = a.Element("CollectiveName")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(coll))
                    {
                        authors.Add(new AuthorName
                        {
                            CollectiveName = coll,
                            Literal = coll,
                            Affiliations = CollectAffiliations(a)
                        });
                        continue;
                    }

                    var last = a.Element("LastName")?.Value?.Trim();
                    var fore = a.Element("ForeName")?.Value?.Trim();
                    var initials = a.Element("Initials")?.Value?.Trim();
                    var orcid = a.Elements("Identifier")
                                    .FirstOrDefault(e => (string?)e.Attribute("Source") == "ORCID")
                                    ?.Value?.Trim();

                    var given = !string.IsNullOrWhiteSpace(fore) ? fore :
                                (!string.IsNullOrWhiteSpace(initials) ? initials : null);

                    if (!string.IsNullOrWhiteSpace(last) || !string.IsNullOrWhiteSpace(given))
                    {
                        authors.Add(new AuthorName
                        {
                            Family = last,
                            Given = given,
                            Orcid = orcid,
                            Affiliations = CollectAffiliations(a)
                        });
                    }
                }
            }

            // Abstract (sections)
            var absSections = new List<AbstractSection>();
            var abstractNode = article.Element("Abstract");
            if (abstractNode is not null)
            {
                // Make 't' statically non-null for the compiler
                foreach (XElement t in abstractNode.Elements("AbstractText"))
                {
                    // XElement.Value never returns null (empty element -> empty string)
                    string raw = t.Value;
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    // Two-step attribute read so the analyzer sees the null-check
                    XAttribute? labAttr = t.Attribute("Label");
                    string? label = labAttr != null ? labAttr.Value : null;

                    absSections.Add(new AbstractSection
                    {
                        Label = string.IsNullOrWhiteSpace(label) ? null : label,
                        Text = NormalizeWhitespace(raw)
                    });
                }
            }

            var abstractPlain = absSections.Count == 0
                ? null
                : string.Join(Environment.NewLine + Environment.NewLine,
                              absSections.Select(s => string.IsNullOrWhiteSpace(s.Label) ? s.Text
                                                                                           : $"{s.Label}: {s.Text}"));

            // Keywords (namespace-agnostic: under <MedlineCitation>, not <Article>)
            var medline = article
                .Ancestors()                                   // any namespace
                .FirstOrDefault(e => e.Name.LocalName == "MedlineCitation");

            List<string> keywords = new();

            if (medline != null)
            {
                keywords = medline
                    .Elements().Where(e => e.Name.LocalName == "KeywordList")
                    .Elements().Where(e => e.Name.LocalName == "Keyword")
                    .Select(k => (k.Value ?? string.Empty).Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }


            // MeSH
            var mesh = x.Descendants("MeshHeadingList")
                       .Elements("MeshHeading")
                       .Select(h =>
                       {
                           var d = h.Element("DescriptorName")?.Value?.Trim();
                           var q = h.Element("QualifierName")?.Value?.Trim();
                           return string.IsNullOrWhiteSpace(q) ? d : $"{d} / {q}";
                       })
                       .Where(s => !string.IsNullOrWhiteSpace(s))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();

            // Pub types
            var pubTypes = article.Element("PublicationTypeList")?
                                .Elements("PublicationType")
                                .Select(p => p.Value?.Trim())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList() ?? new List<string>();

            // Language / Country
            var language = article.Element("Language")?.Value?.Trim();
            var country = x.Descendants("MedlineJournalInfo")
                            .Elements("Country")
                            .Select(e => e.Value?.Trim())
                            .FirstOrDefault();

            // Grants
            var grants = article.Element("GrantList")?
                                .Elements("Grant")
                                .Select(g => new GrantInfo
                                {
                                    GrantId = g.Element("GrantID")?.Value?.Trim(),
                                    Agency = g.Element("Agency")?.Value?.Trim(),
                                    Country = g.Element("Country")?.Value?.Trim()
                                })
                                .ToList() ?? new List<GrantInfo>();

            // References / comments-corrections
            var refs = x.Descendants("Reference")
                        .Elements("ArticleIdList")
                        .Elements("ArticleId")
                        .Where(e => ((string?)e.Attribute("IdType")) == "pubmed")
                        .Select(e => e.Value?.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct()
                        .ToList();

            var comments = x.Descendants("CommentsCorrections")
                            .Select(e => e.Value?.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct()
                            .ToList();

            // Affiliations (strings only)
            var allAffiliations =
                authors.SelectMany(a => a.Affiliations)
                       .Concat(article.Elements("Affiliation").Select(a => a.Value))
                       .Concat(article.Elements("AffiliationInfo").Elements("Affiliation").Select(a => a.Value))
                       .Where(s => !string.IsNullOrWhiteSpace(s))
                       .Select(NormalizeWhitespace)
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();

            return new PublicationRecord
            {
                Title = title,
                JournalTitle = jTitle,
                JournalIsoAbbrev = jIso,
                Volume = volume,
                Issue = issue,
                Pages = pages,
                Year = year,
                PublishedPrint = printDate,
                PublishedEpub = epubDate,
                Doi = doi,
                Pmid = pmid,
                Pmcid = pmcid,
                UrlPubMed = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/",
                Authors = authors,
                AbstractSections = absSections,
                AbstractPlain = abstractPlain,
                Keywords = keywords,
                MeshHeadings = mesh,
                PublicationTypes = pubTypes,
                Language = language,
                Country = country,
                Grants = grants,
                ReferencedPmids = refs,
                CommentsCorrections = comments,
                Affiliations = allAffiliations
            };
        }

        private static (int? count, IReadOnlyList<string> list) DefaultCitedBy =>
            (null, Array.Empty<string>());

        private static async Task<(int? count, IReadOnlyList<string> list)> TryGetCitedByAsync(string pmid, CancellationToken ct)
        {
            try
            {
                var url = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/elink.fcgi?dbfrom=pubmed&linkname=pubmed_pubmed_citedin&id={pmid}";
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return DefaultCitedBy;

                var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var ids = xml.Descendants("LinkSetDb")
                             .Where(db => (string?)db.Element("LinkName") == "pubmed_pubmed_citedin")
                             .Elements("Link")
                             .Elements("Id")
                             .Select(e => e.Value?.Trim())
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .Distinct()
                             .ToList();

                return (ids.Count, ids);
            }
            catch
            {
                return DefaultCitedBy;
            }
        }

        private static IReadOnlyList<string> CollectAffiliations(XElement authorNode)
        {
            return authorNode.Elements("AffiliationInfo")
                             .Elements("Affiliation")
                             .Select(a => NormalizeWhitespace(a.Value))
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();
        }

        private static string NormalizeWhitespace(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return string.Join(" ", s.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
        }

        private static int? TryYear(XElement article)
        {
            var yearStr =
                article.Descendants("Year").Select(e => e.Value?.Trim()).FirstOrDefault()
                ?? article.Descendants("MedlineDate").Select(e => e.Value?.Trim()).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(yearStr)) return null;

            var first4 = new string(yearStr.Take(4).ToArray());
            return int.TryParse(first4, NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? y : (int?)null;
        }

        private static (DateOnly?, DateOnly?) TryDates(XElement article)
        {
            DateOnly? printDate = null, epubDate = null;

            static DateOnly? ParseDateFrom(XElement parent)
            {
                var year = parent.Element("Year")?.Value;
                var month = parent.Element("Month")?.Value;
                var day = parent.Element("Day")?.Value;

                if (!int.TryParse(year, out var y)) return null;
                var m = ParseMonth(month);
                var d = int.TryParse(day, out var dd) ? dd : 1;

                try { return new DateOnly(y, m, d); } catch { return null; }
            }

            var ji = article.Element("Journal")?.Element("JournalIssue");
            if (ji is not null)
            {
                var pubDate = ji.Element("PubDate");
                if (pubDate is not null)
                    printDate = ParseDateFrom(pubDate);
            }

            var ad = article.Element("ArticleDate");
            if (ad is not null)
                epubDate = ParseDateFrom(ad);

            return (printDate, epubDate);
        }

        private static int ParseMonth(string? month)
        {
            if (string.IsNullOrWhiteSpace(month)) return 1;
            if (int.TryParse(month, out var n) && n is >= 1 and <= 12) return n;

            try { return DateTime.ParseExact(month[..Math.Min(3, month.Length)], "MMM", CultureInfo.InvariantCulture).Month; }
            catch { return 1; }
        }

        // Helper that works whether PublicationRecord is a class or a record
        private static PublicationRecord WithCitedBy(PublicationRecord r, int? count, IReadOnlyList<string> cited)
        {
            return new PublicationRecord
            {
                Title = r.Title,
                JournalTitle = r.JournalTitle,
                JournalIsoAbbrev = r.JournalIsoAbbrev,
                Volume = r.Volume,
                Issue = r.Issue,
                Pages = r.Pages,
                Year = r.Year,
                PublishedPrint = r.PublishedPrint,
                PublishedEpub = r.PublishedEpub,
                Doi = r.Doi,
                Pmid = r.Pmid,
                Pmcid = r.Pmcid,
                UrlPubMed = r.UrlPubMed,
                Authors = r.Authors,
                AbstractSections = r.AbstractSections,
                AbstractPlain = r.AbstractPlain,
                Keywords = r.Keywords,
                MeshHeadings = r.MeshHeadings,
                PublicationTypes = r.PublicationTypes,
                Language = r.Language,
                Country = r.Country,
                Grants = r.Grants,
                ReferencedPmids = r.ReferencedPmids,
                CommentsCorrections = r.CommentsCorrections,
                Affiliations = r.Affiliations,
                CitedByCount = count,
                CitedByPmids = cited
            };
        }
    }
}
