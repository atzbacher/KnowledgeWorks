#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Structured metadata representing a table referenced by the extraction.</summary>
    public sealed class DataExtractionTable : DataExtractionArtifact
    {
        [JsonPropertyName("table_label")]
        public string? TableLabel { get; init; }

        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("column_hint")]
        public int? ColumnCountHint { get; init; }

        [JsonPropertyName("dictionary_path")]
        public string? DictionaryPath { get; init; }

        [JsonPropertyName("friendly_name")]
        public string? FriendlyName { get; init; }

        [JsonPropertyName("image_path")]
        public string? ImagePath { get; init; }

        [JsonPropertyName("image_provenance_hash")]
        public string? ImageProvenanceHash { get; init; }

        [JsonPropertyName("page_positions")]
        public List<DataExtractionPagePosition> PagePositions { get; init; } = new();
    }
}
