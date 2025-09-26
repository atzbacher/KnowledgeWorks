#nullable enable
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Describes a rectangular region within a staged PDF page, using normalized
    /// coordinates so downstream consumers can locate the source content.
    /// </summary>
    public sealed class DataExtractionRegion
    {
        [JsonPropertyName("page")] 
        public int PageNumber { get; init; }

        [JsonPropertyName("x")] 
        public double X { get; init; }

        [JsonPropertyName("y")] 
        public double Y { get; init; }

        [JsonPropertyName("width")] 
        public double Width { get; init; }

        [JsonPropertyName("height")] 
        public double Height { get; init; }

        [JsonPropertyName("label")] 
        public string? Label { get; init; }
    }
}
