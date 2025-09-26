#nullable enable
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
    }
}
