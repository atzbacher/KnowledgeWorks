using System;

namespace LM.Core.Models.Pdf
{
    /// <summary>
    /// Snapshot of annotation metadata captured from the PDF bridge layer.
    /// </summary>
    public sealed class PdfAnnotationBridgeMetadata
    {
        public PdfAnnotationBridgeMetadata(string annotationId, string? text, string? note)
        {
            if (string.IsNullOrWhiteSpace(annotationId))
            {
                throw new ArgumentException("Annotation identifier must be provided.", nameof(annotationId));
            }

            AnnotationId = annotationId.Trim();
            Text = NormalizeOptional(text);
            Note = NormalizeOptional(note);
        }

        /// <summary>
        /// Gets the unique annotation identifier provided by the viewer bridge.
        /// </summary>
        public string AnnotationId { get; }

        /// <summary>
        /// Gets the sanitized text snippet associated with the annotation, if any.
        /// </summary>
        public string? Text { get; }

        /// <summary>
        /// Gets the sanitized free-form note captured for the annotation, if any.
        /// </summary>
        public string? Note { get; }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
