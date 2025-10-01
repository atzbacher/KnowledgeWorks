using System;
using System.Collections.Generic;
using System.Globalization;
using LM.Core.Models.Pdf;
using LM.HubSpoke.Models;

namespace LM.Infrastructure.Pdf
{
    /// <summary>
    /// Projects bridge-provided annotation metadata into the hook serialization model.
    /// </summary>
    internal static class PdfAnnotationMetadataProjector
    {
        public static List<PdfAnnotationMetadata> CreateMetadataList(IReadOnlyList<PdfAnnotationBridgeMetadata> annotations)
        {
            if (annotations is null || annotations.Count == 0)
            {
                return new List<PdfAnnotationMetadata>();
            }

            var map = new Dictionary<string, PdfAnnotationMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var annotation in annotations)
            {
                if (annotation is null || string.IsNullOrWhiteSpace(annotation.AnnotationId))
                {
                    continue;
                }

                var normalizedId = annotation.AnnotationId.Trim();
                map[normalizedId] = new PdfAnnotationMetadata
                {
                    AnnotationId = normalizedId,
                    Text = annotation.Text,
                    Note = annotation.Note,
                    Color = CreateColorMetadata(annotation.ColorHex)
                };
            }

            return new List<PdfAnnotationMetadata>(map.Values);
        }

        private static PdfAnnotationColorMetadata? CreateColorMetadata(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return null;
            }

            if (!TryParseHex(colorHex, out var red, out var green, out var blue))
            {
                return null;
            }

            return new PdfAnnotationColorMetadata
            {
                Red = red,
                Green = green,
                Blue = blue
            };
        }

        private static bool TryParseHex(string? value, out int red, out int green, out int blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
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
                return false;
            }

            if (!byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                || !byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                || !byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }

            red = r;
            green = g;
            blue = b;
            return true;
        }
    }
}
