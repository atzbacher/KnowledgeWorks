#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    public sealed class PdfAnnotationsHook
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("overlayPath")]
        public string OverlayPath { get; init; } = string.Empty;

        [JsonPropertyName("previews")]
        public List<PdfAnnotationPreview> Previews { get; init; } = new();
    }

    public sealed class PdfAnnotationPreview
    {
        [JsonPropertyName("annotationId")]
        public string AnnotationId { get; init; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; init; } = string.Empty;
    }
}
