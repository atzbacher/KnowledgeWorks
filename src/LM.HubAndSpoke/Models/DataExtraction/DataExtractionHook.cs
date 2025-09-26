#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Root payload persisted to the extraction hash tree. Captures who extracted
    /// the evidence and the structured data points gathered during review.
    /// </summary>
    public sealed class DataExtractionHook
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; } = "1.0.0";

        [JsonPropertyName("extracted_by")]
        public string ExtractedBy { get; init; } = string.Empty;

        [JsonPropertyName("extracted_at_utc")]
        [JsonConverter(typeof(UtcDateTimeConverter))]
        public DateTime ExtractedAtUtc { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("populations")]
        public List<DataExtractionPopulation> Populations { get; init; } = new();

        [JsonPropertyName("interventions")]
        public List<DataExtractionIntervention> Interventions { get; init; } = new();

        [JsonPropertyName("endpoints")]
        public List<DataExtractionEndpoint> Endpoints { get; init; } = new();

        [JsonPropertyName("figures")]
        public List<DataExtractionFigure> Figures { get; init; } = new();

        [JsonPropertyName("tables")]
        public List<DataExtractionTable> Tables { get; init; } = new();

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }
}
