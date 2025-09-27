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

        [JsonPropertyName("study_design")]
        public string? StudyDesign { get; init; }

        [JsonPropertyName("study_setting")]
        public string? StudySetting { get; init; }

        [JsonPropertyName("site_count")]
        public int? SiteCount { get; init; }

        [JsonPropertyName("trial_classification")]
        public string? TrialClassification { get; init; }

        [JsonPropertyName("is_registry_study")]
        public bool? IsRegistryStudy { get; init; }

        [JsonPropertyName("is_cohort_study")]
        public bool? IsCohortStudy { get; init; }

        [JsonPropertyName("geography_scope")]
        public string? GeographyScope { get; init; }
    }
}
