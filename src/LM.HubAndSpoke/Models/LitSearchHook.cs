#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LM.Core.Utils;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Hook payload persisted alongside LitSearch entries. Captures the query definition
    /// and a history of executed runs so the hub/spoke pipeline can rebuild entries later.
    /// </summary>
    public sealed class LitSearchHook
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; init; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        [JsonPropertyName("from")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? From { get; init; }

        [JsonPropertyName("to")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? To { get; init; }

        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; init; }

        [JsonPropertyName("createdUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("keywords")]
        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("derivedFromEntryId")]
        public string? DerivedFromEntryId { get; init; }

        [JsonPropertyName("runs")]
        public List<LitSearchRun> Runs { get; init; } = new();
    }

    /// <summary>
    /// Snapshot of a single literature search execution.
    /// </summary>
    public sealed class LitSearchRun
    {
        [JsonPropertyName("runId")]
        public string RunId { get; init; } = IdGen.NewId();

        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; init; } = string.Empty;

        [JsonPropertyName("from")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? From { get; init; }

        [JsonPropertyName("to")]
        [JsonConverter(typeof(NullableUtcDateTimeConverter))]
        public DateTime? To { get; init; }

        [JsonPropertyName("runUtc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime RunUtc { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("totalHits")]
        public int TotalHits { get; init; }

        [JsonPropertyName("executedBy")]
        public string? ExecutedBy { get; init; }

        [JsonPropertyName("rawAttachments")]
        public List<string> RawAttachments { get; init; } = new();

        [JsonPropertyName("importedEntryIds")]
        public List<string> ImportedEntryIds { get; init; } = new();
    }
}
