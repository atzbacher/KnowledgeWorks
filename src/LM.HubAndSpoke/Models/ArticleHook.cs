#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Article hook file (e.g., entries/<EntryId>/hooks/article.json).
    /// Mirrors PubMed fields + adds Assets for files related to this entry.
    /// </summary>
    public sealed class ArticleHook
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; } = "1.0.0";

        // ---------- PubMed payload ----------

        [JsonPropertyName("identifier")]
        public ArticleIdentifier Identifier { get; init; } = new();

        [JsonPropertyName("journal")]
        public JournalInfo Journal { get; init; } = new();

        [JsonPropertyName("article")]
        public ArticleDetails Article { get; init; } = new();

        [JsonPropertyName("abstract")]
        public ArticleAbstract? Abstract { get; init; }

        [JsonPropertyName("authors")]
        public List<Author> Authors { get; init; } = new();

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; init; } = new();

        [JsonPropertyName("meshHeadings")]
        public List<MeshHeading> MeshHeadings { get; init; } = new();

        [JsonPropertyName("chemicals")]
        public List<Chemical> Chemicals { get; init; } = new();

        [JsonPropertyName("grants")]
        public List<Grant> Grants { get; init; } = new();

        [JsonPropertyName("references")]
        public List<Citation> References { get; init; } = new();

        [JsonPropertyName("history")]
        public PublicationHistory History { get; init; } = new();

        [JsonPropertyName("medline")]
        public MedlineInfo Medline { get; init; } = new();

        [JsonPropertyName("copyright")]
        public string? Copyright { get; init; }

        [JsonPropertyName("conflictOfInterest")]
        public string? ConflictOfInterest { get; init; }

        // ---------- Attachments for this entry ----------

        [JsonPropertyName("assets")]
        public List<ArticleAsset> Assets { get; init; } = new();
    }

    // ======================== Attachments ========================

    public sealed class ArticleAsset
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty; // e.g., "<64hex>.pdf"

        [JsonPropertyName("original_filename")]
        public string? OriginalFilename { get; init; }

        [JsonPropertyName("original_folder_path")]
        public string? OriginalFolderPath { get; init; }

        [JsonPropertyName("storage_path")]
        public string StoragePath { get; init; } = string.Empty; // "library/ab/cd/<hash>.<ext>"

        [JsonPropertyName("hash")]
        public string Hash { get; init; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string ContentType { get; init; } = string.Empty;

        [JsonPropertyName("bytes")]
        public long Bytes { get; init; }

        [JsonPropertyName("purpose")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ArticleAssetPurpose Purpose { get; init; } = ArticleAssetPurpose.Manuscript;
    }

    public enum ArticleAssetPurpose
    {
        Manuscript,
        Supplement
    }

    // ======================== PubMed shapes ========================

    public sealed class ArticleIdentifier
    {
        [JsonPropertyName("pmid")]
        public string PMID { get; init; } = string.Empty;

        [JsonPropertyName("doi")]
        public string? DOI { get; init; }

        [JsonPropertyName("pii")]
        public string? PII { get; init; }

        [JsonPropertyName("pmcid")]
        public string? PMCID { get; init; }

        [JsonPropertyName("otherIds")]
        public Dictionary<string, string> OtherIds { get; init; } = new();
    }

    public sealed class JournalInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("isoAbbreviation")]
        public string? ISOAbbreviation { get; init; }

        [JsonPropertyName("issn")]
        public string? ISSN { get; init; }

        [JsonPropertyName("issnPrint")]
        public string? ISSNPrint { get; init; }

        [JsonPropertyName("issnElectronic")]
        public string? ISSNElectronic { get; init; }

        [JsonPropertyName("issue")]
        public JournalIssue Issue { get; init; } = new();

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("nlmUniqueId")]
        public string? NlmUniqueId { get; init; }
    }

    public sealed class JournalIssue
    {
        [JsonPropertyName("volume")]
        public string? Volume { get; init; }

        [JsonPropertyName("number")]
        public string? Number { get; init; }

        [JsonPropertyName("pubDate")]
        public PartialDate? PubDate { get; init; }
    }

    public sealed class ArticleDetails
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; init; } = new();

        [JsonPropertyName("publicationTypes")]
        public List<string> PublicationTypes { get; init; } = new();

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PubStatus Status { get; init; } = PubStatus.Unknown;

        [JsonPropertyName("dates")]
        public ArticleDates Dates { get; init; } = new();
    }

    public sealed class Pagination
    {
        [JsonPropertyName("startPage")]
        public string? StartPage { get; init; }

        [JsonPropertyName("endPage")]
        public string? EndPage { get; init; }

        [JsonPropertyName("articleNumber")]
        public string? ArticleNumber { get; init; }
    }

    public sealed class ArticleDates
    {
        [JsonPropertyName("electronic")]
        public DateTimeOffset? Electronic { get; init; }

        [JsonPropertyName("print")]
        public DateTimeOffset? Print { get; init; }

        [JsonPropertyName("received")]
        public DateTimeOffset? Received { get; init; }

        [JsonPropertyName("revised")]
        public DateTimeOffset? Revised { get; init; }

        [JsonPropertyName("accepted")]
        public DateTimeOffset? Accepted { get; init; }
    }

    public enum PubStatus
    {
        Unknown = 0,
        AheadOfPrint,
        EPublish,
        PPublish,
        Medline,
        PubMedNotMedline
    }

    public sealed class ArticleAbstract
    {
        [JsonPropertyName("sections")]
        public List<AbstractSection> Sections { get; init; } = new();

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    public sealed class AbstractSection
    {
        [JsonPropertyName("label")]
        public string? Label { get; init; }

        // CLR name unified to Text; JSON remains "content" for backward compatibility
        [JsonPropertyName("content")]
        public string Text { get; init; } = string.Empty;
    }

    public sealed class Author
    {
        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("foreName")]
        public string? ForeName { get; init; }

        [JsonPropertyName("initials")]
        public string? Initials { get; init; }

        [JsonPropertyName("orcid")]
        public string? ORCID { get; init; }

        [JsonPropertyName("affiliations")]
        public List<Affiliation> Affiliations { get; init; } = new();
    }

    public sealed class Affiliation
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; init; }
    }

    public sealed class MeshHeading
    {
        [JsonPropertyName("descriptor")]
        public string Descriptor { get; init; } = string.Empty;

        [JsonPropertyName("qualifiers")]
        public List<string> Qualifiers { get; init; } = new();

        [JsonPropertyName("majorTopic")]
        public bool MajorTopic { get; init; }
    }

    public sealed class Chemical
    {
        [JsonPropertyName("registryNumber")]
        public string? RegistryNumber { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Grant
    {
        [JsonPropertyName("grantId")]
        public string? GrantId { get; init; }

        [JsonPropertyName("agency")]
        public string? Agency { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }
    }

    public sealed class Citation
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;

        [JsonPropertyName("pmid")]
        public string? PMID { get; init; }

        [JsonPropertyName("doi")]
        public string? DOI { get; init; }
    }

    public sealed class PublicationHistory
    {
        [JsonPropertyName("received")]
        public DateTimeOffset? Received { get; init; }

        [JsonPropertyName("revised")]
        public DateTimeOffset? Revised { get; init; }

        [JsonPropertyName("accepted")]
        public DateTimeOffset? Accepted { get; init; }

        [JsonPropertyName("pubmed")]
        public DateTimeOffset? PubMed { get; init; }

        [JsonPropertyName("medline")]
        public DateTimeOffset? Medline { get; init; }

        [JsonPropertyName("entrez")]
        public DateTimeOffset? Entrez { get; init; }
    }

    public sealed class MedlineInfo
    {
        [JsonPropertyName("citationSubset")]
        public string? CitationSubset { get; init; }

        [JsonPropertyName("publicationStatusRaw")]
        public string? PublicationStatusRaw { get; init; }
    }

    /// <summary>
    /// Partial date helper (year-only / year-month / full date).
    /// </summary>
    public sealed class PartialDate
    {
        [JsonPropertyName("year")]
        public int Year { get; init; }

        [JsonPropertyName("month")]
        public int? Month { get; init; }

        [JsonPropertyName("day")]
        public int? Day { get; init; }

        public DateTime? ToDateTimeOrNull()
        {
            try
            {
                if (Month is null) return new DateTime(Year, 1, 1);
                if (Day is null) return new DateTime(Year, Month.Value, 1);
                return new DateTime(Year, Month.Value, Day.Value);
            }
            catch { return null; }
        }
    }
}
