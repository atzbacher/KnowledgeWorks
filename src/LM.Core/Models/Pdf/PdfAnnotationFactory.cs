using System;
using System.Collections.Generic;

namespace LM.Core.Models.Pdf;

internal sealed class PdfAnnotationFactory : IPdfAnnotationFactory
{
    public PdfAnnotation Create(
        Guid id,
        DateTimeOffset createdAt,
        int pageIndex,
        IEnumerable<PdfAnnotationRect> rectangles,
        string? selectedText = null,
        PdfAnnotationColor? color = null,
        string? notes = null,
        IEnumerable<string>? tags = null,
        PdfAnnotationPreviewImage? preview = null,
        DateTimeOffset? modifiedAt = null)
    {
        ArgumentNullException.ThrowIfNull(rectangles);

        var normalizedRects = PdfAnnotationCollectionUtilities.NormalizeRectangles(rectangles);
        var normalizedTags = PdfAnnotationCollectionUtilities.NormalizeTags(tags);

        return new PdfAnnotation(
            id,
            createdAt,
            modifiedAt ?? createdAt,
            pageIndex,
            normalizedRects,
            selectedText,
            color,
            notes,
            normalizedTags,
            preview);
    }

    public PdfAnnotation UpdateRectangles(PdfAnnotation annotation, IEnumerable<PdfAnnotationRect> rectangles)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(rectangles);

        var normalized = PdfAnnotationCollectionUtilities.NormalizeRectangles(rectangles);
        return annotation with { Rectangles = normalized, ModifiedAt = DateTimeOffset.UtcNow };
    }

    public PdfAnnotation UpdateTags(PdfAnnotation annotation, IEnumerable<string>? tags)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        var normalized = PdfAnnotationCollectionUtilities.NormalizeTags(tags);
        return annotation with { Tags = normalized, ModifiedAt = DateTimeOffset.UtcNow };
    }
}
