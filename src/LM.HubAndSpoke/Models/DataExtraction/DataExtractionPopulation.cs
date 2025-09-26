#nullable enable
using System;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Participant grouping captured during data extraction.</summary>
    public sealed class DataExtractionPopulation
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("label")]
        public string Label { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("inclusion_criteria")]
        public string? InclusionCriteria { get; init; }

        [JsonPropertyName("exclusion_criteria")]
        public string? ExclusionCriteria { get; init; }

        [JsonPropertyName("sample_size")]
        public int? SampleSize { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }
}
