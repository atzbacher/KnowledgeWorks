using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LM.Core.Models.Pdf;

internal static class PdfAnnotationCollectionUtilities
{
    public static ImmutableArray<PdfAnnotationRect> NormalizeRectangles(IEnumerable<PdfAnnotationRect> rectangles)
    {
        if (rectangles is null)
        {
            throw new ArgumentNullException(nameof(rectangles));
        }

        var normalized = rectangles
            .Select(rect => rect ?? throw new ArgumentNullException(nameof(rectangles), "Rectangle entries cannot be null."))
            .Select(rect => new PdfAnnotationRect(rect.X, rect.Y, rect.Width, rect.Height))
            .ToImmutableArray();

        return normalized.IsDefault ? ImmutableArray<PdfAnnotationRect>.Empty : normalized;
    }

    public static ImmutableArray<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return ImmutableArray<string>.Empty;
        }

        var normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        return normalizedTags.IsDefault ? ImmutableArray<string>.Empty : normalizedTags;
    }
}
