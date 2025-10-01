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

        [JsonPropertyName("annotations")]
        public List<PdfAnnotationMetadata> Annotations { get; init; } = new();

        [JsonPropertyName("previews")]
        public List<PdfAnnotationPreview> Previews { get; init; } = new();
    }

    public sealed class PdfAnnotationMetadata
    {
        [JsonPropertyName("annotationId")]
        public string AnnotationId { get; init; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }

    public sealed class PdfAnnotationPreview
    {
        [JsonPropertyName("annotationId")]
        public string AnnotationId { get; init; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; init; } = string.Empty;
    }
}
