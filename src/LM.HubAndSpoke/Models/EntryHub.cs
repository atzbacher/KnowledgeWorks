using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using LM.Core.Abstractions;           // IEntryStore, IWorkSpaceService, IFileStorageRepository, IHasher, IContentExtractor
using LM.Core.Models;                 // Entry, Attachment, EntryType
using LM.Core.Models.Filters;         // EntryFilter
using LM.Core.Utils;                  // IdGen   


namespace LM.HubSpoke.Models
{
    // ===================== HUB =====================

    public sealed class EntryHub
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; } = "1.0.0";

        [JsonPropertyName("entry_id")]
        public string EntryId { get; init; } = string.Empty; // ULID string recommended

        [JsonPropertyName("display_title")]
        public string DisplayTitle { get; init; } = string.Empty;

        // --- NEW: creation & origin ---

        /// <summary>How this entry was created (search-import vs manual).</summary>
        [JsonPropertyName("creation_method")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CreationMethod CreationMethod { get; init; } = CreationMethod.Manual;

        /// <summary>Provenance from an org perspective: internal vs external.</summary>
        [JsonPropertyName("origin")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EntryOrigin Origin { get; init; } = EntryOrigin.External;

        /// <summary>Primary purpose (what this entry mainly represents).</summary>
        [JsonPropertyName("primary_purpose")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EntryPurpose PrimaryPurpose { get; init; } = EntryPurpose.Unknown;

        /// <summary>Was the purpose inferred (first asset/hook) or set manually?</summary>
        [JsonPropertyName("primary_purpose_source")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PurposeSource PrimaryPurposeSource { get; init; } = PurposeSource.Inferred;

        // --- timestamps & authorship ---

        [JsonPropertyName("created_utc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime CreatedUtc { get; init; }

        [JsonPropertyName("updated_utc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime UpdatedUtc { get; init; }

        [JsonPropertyName("created_by")]
        public PersonRef CreatedBy { get; init; } = PersonRef.Unknown;

        [JsonPropertyName("updated_by")]
        public PersonRef UpdatedBy { get; init; } = PersonRef.Unknown;

        [JsonPropertyName("last_activity_utc")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? LastActivityUtc { get; init; }

        // --- tags & hooks ---

        /// <summary>Hierarchical leaf tags only; ancestors resolved via registry/DB.</summary>
        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

        /// <summary>Pointers to spoke files. Heavy payloads & assets live under hooks.</summary>
        [JsonPropertyName("hooks")]
        public EntryHooks Hooks { get; init; } = new();

        /// <summary>Optional counters for fast UI; edges live in hooks/relations.jsonl.</summary>
        [JsonPropertyName("relations_summary")]
        public EntryRelationsSummary? RelationsSummary { get; init; }

        /// <summary>Bumped on each save to avoid lost updates.</summary>
        [JsonPropertyName("concurrency_stamp")]
        public string ConcurrencyStamp { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("flags")]
        public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
    }

    public sealed class EntryHooks
    {
        [JsonPropertyName("article")]
        public string? Article { get; init; }            // "hooks/article.json"

        [JsonPropertyName("lit_search")]
        public string? LitSearch { get; init; }          // "hooks/litsearch.json"

        [JsonPropertyName("trial")]
        public string? Trial { get; init; }              // "hooks/trial.json"

        [JsonPropertyName("document")]
        public string? Document { get; init; }           // "hooks/document.json"  <-- NEW generic document hook

        [JsonPropertyName("data_extraction")]
        public string? DataExtraction { get; init; }     // "extraction/ab/cd/<sha256>.json"

        [JsonPropertyName("relations")]
        public string? Relations { get; init; }          // "hooks/relations.jsonl"

        [JsonPropertyName("history")]
        public string? History { get; init; }            // "hooks/history.jsonl"

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }              // "hooks/notes.json"

        [JsonPropertyName("provenance")]
        public string? Provenance { get; init; }         // "hooks/provenance.json"

        [JsonPropertyName("search_hits")]
        public string? SearchHits { get; init; }         // "hooks/search_hits.jsonl"
    }

    public sealed class EntryRelationsSummary
    {
        [JsonPropertyName("related_count")]
        public int RelatedCount { get; init; }

        [JsonPropertyName("variant_of_count")]
        public int VariantOfCount { get; init; }

        [JsonPropertyName("relations_updated_utc")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? RelationsUpdatedUtc { get; init; }
    }

    public readonly record struct PersonRef(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("display_name")] string? DisplayName)
    {
        public static PersonRef Unknown => new("unknown", null);
    }

    // ===================== ENUMS =====================

    public enum CreationMethod { Manual, Search }
    public enum EntryOrigin { External, Internal }

    /// <summary>Keep this limited and high level; detailed semantics live in hooks.</summary>
    public enum EntryPurpose
    {
        Unknown = 0,
        Manuscript,
        Trial,
        Document,      // generic non-article artifact (presentations, marketing PDFs, SOPs, etc.)
        Dataset,
        Code
    }

    public enum PurposeSource { Inferred, Manual }

    // ===================== DOCUMENT HOOK =====================

    /// <summary>
    /// Generic document hook for non-article materials (presentations, marketing, SOPs, reports).
    /// All assets referenced here; nothing stored in the hub.
    /// </summary>
    public sealed class DocumentHook
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; } = "1.0.0";

        [JsonPropertyName("document_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DocumentType DocumentType { get; init; } = DocumentType.Other;

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("version")]
        public string? Version { get; init; }

        [JsonPropertyName("owner")]
        public string? Owner { get; init; }  // team/person

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("effective_utc")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? EffectiveUtc { get; init; }

        [JsonPropertyName("expires_utc")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? ExpiresUtc { get; init; }

        [JsonPropertyName("assets")]
        public IReadOnlyList<AssetRef> Assets { get; init; } = Array.Empty<AssetRef>();
    }

    public enum DocumentType
    {
        Presentation,
        MarketingMaterial,
        Report,
        StandardOperatingProcedure,
        Whitepaper,
        Poster,
        Other
    }

    public sealed class AssetRef
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty; // e.g., "primary_pdf", "slides", "image"

        [JsonPropertyName("hash")]
        public string Hash { get; init; } = string.Empty; // "sha256-<64hex>"

        [JsonPropertyName("storage_path")]
        public string StoragePath { get; init; } = string.Empty; // "library/ab/cd/<hash>.<ext>"

        [JsonPropertyName("content_type")]
        public string ContentType { get; init; } = string.Empty;

        [JsonPropertyName("bytes")]
        public long Bytes { get; init; }

        [JsonPropertyName("original_filename")]
        public string? OriginalFilename { get; init; }
    }

    // ===================== UTC CONVERTERS & OPTIONS =====================

    public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return default;
            var dt = DateTime.Parse(s!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };
        }
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"));
        }
    }

    public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            var dt = DateTime.Parse(s!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null) { writer.WriteNullValue(); return; }
            var utc = value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"));
        }
    }

    public static class JsonStd
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };
    }
}
