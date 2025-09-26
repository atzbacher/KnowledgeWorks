#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Outcome or endpoint recorded for a specific arm/population pairing.</summary>
    public sealed class DataExtractionEndpoint
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("timepoint")]
        public string? Timepoint { get; init; }

        [JsonPropertyName("measure")]
        public string? Measure { get; init; }

        [JsonPropertyName("population_ids")]
        public List<string> PopulationIds { get; init; } = new();

        [JsonPropertyName("intervention_ids")]
        public List<string> InterventionIds { get; init; } = new();

        [JsonPropertyName("result_summary")]
        public string? ResultSummary { get; init; }

        [JsonPropertyName("effect_size")]
        public string? EffectSize { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }
}
