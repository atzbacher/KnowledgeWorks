using System;

namespace LM.Core.Models.Pdf
{
    /// <summary>
    /// Snapshot of annotation metadata captured from the PDF bridge layer.
    /// </summary>
    public sealed class PdfAnnotationBridgeMetadata
    {
        public PdfAnnotationBridgeMetadata(string annotationId, string? text, string? note, string? colorHex = null)
        {
            if (string.IsNullOrWhiteSpace(annotationId))
            {
                throw new ArgumentException("Annotation identifier must be provided.", nameof(annotationId));
            }

            AnnotationId = annotationId.Trim();
            Text = NormalizeOptional(text);
            Note = NormalizeOptional(note);
            ColorHex = NormalizeColor(colorHex);
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

        /// <summary>
        /// Gets the normalized RGB color representation associated with the annotation highlight, if captured.
        /// </summary>
        public string? ColorHex { get; }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length == 8)
            {
                trimmed = trimmed[2..];
            }

            if (trimmed.Length != 6)
            {
                return null;
            }

            for (var i = 0; i < trimmed.Length; i++)
            {
                if (!IsHexDigit(trimmed[i]))
                {
                    return null;
                }
            }

            return string.Create(7, trimmed, static (span, state) =>
            {
                span[0] = '#';
                for (var i = 0; i < state.Length; i++)
                {
                    span[i + 1] = char.ToUpperInvariant(state[i]);
                }
            });
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
        }
    }
}
