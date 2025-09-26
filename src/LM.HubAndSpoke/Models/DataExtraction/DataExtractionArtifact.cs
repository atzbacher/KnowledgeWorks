#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Shared metadata for figures and tables captured during extraction.</summary>
    public abstract class DataExtractionArtifact
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("caption")]
        public string? Caption { get; init; }

        [JsonPropertyName("source_path")]
        public string? SourcePath { get; init; }

        [JsonPropertyName("pages")]
        public List<string> Pages { get; init; } = new();

        [JsonPropertyName("regions")]
        public List<DataExtractionRegion> Regions { get; init; } = new();

        [JsonPropertyName("linked_endpoint_ids")]
        public List<string> LinkedEndpointIds { get; init; } = new();

        [JsonPropertyName("linked_intervention_ids")]
        public List<string> LinkedInterventionIds { get; init; } = new();

        [JsonPropertyName("provenance_hash")]
        public string ProvenanceHash { get; init; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; init; } = new();
    }
}
