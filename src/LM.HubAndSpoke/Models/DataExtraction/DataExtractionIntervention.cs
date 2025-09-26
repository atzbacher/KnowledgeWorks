#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Intervention arm tracked in the extraction payload.</summary>
    public sealed class DataExtractionIntervention
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("population_ids")]
        public List<string> PopulationIds { get; init; } = new();

        [JsonPropertyName("dosage")]
        public string? Dosage { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }
}
