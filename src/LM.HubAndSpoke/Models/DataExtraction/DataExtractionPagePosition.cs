#nullable enable
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>Absolute location for a table region expressed in PDF points.</summary>
    public sealed class DataExtractionPagePosition
    {
        [JsonPropertyName("page")]
        public int PageNumber { get; init; }

        [JsonPropertyName("left")]
        public double Left { get; init; }

        [JsonPropertyName("top")]
        public double Top { get; init; }

        [JsonPropertyName("width")]
        public double Width { get; init; }

        [JsonPropertyName("height")]
        public double Height { get; init; }

        [JsonPropertyName("page_width")]
        public double PageWidth { get; init; }

        [JsonPropertyName("page_height")]
        public double PageHeight { get; init; }
    }
}
