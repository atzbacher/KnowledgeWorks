#nullable enable
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Structured metadata representing a figure referenced by the extraction.</summary>
    public sealed class DataExtractionFigure : DataExtractionArtifact
    {
        [JsonPropertyName("figure_label")]
        public string? FigureLabel { get; init; }
    }
}
