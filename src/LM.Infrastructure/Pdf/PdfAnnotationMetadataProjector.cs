using System;
using System.Collections.Generic;
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
                    Note = annotation.Note
                };
            }

            return new List<PdfAnnotationMetadata>(map.Values);
        }
    }
}
