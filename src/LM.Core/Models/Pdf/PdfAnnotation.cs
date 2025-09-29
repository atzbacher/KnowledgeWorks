using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LM.Core.Models.Pdf;

internal sealed record PdfAnnotation : IPdfAnnotation
{
    public PdfAnnotation(
        Guid id,
        DateTimeOffset createdAt,
        DateTimeOffset modifiedAt,
        int pageIndex,
        ImmutableArray<PdfAnnotationRect> rectangles,
        string? selectedText,
        PdfAnnotationColor? color,
        string? notes,
        ImmutableArray<string> tags,
        PdfAnnotationPreviewImage? preview)
    {
        Id = PdfAnnotationValidators.EnsureValidId(id);
        CreatedAt = createdAt;
        ModifiedAt = PdfAnnotationValidators.EnsureValidModification(createdAt, modifiedAt);
        PageIndex = PdfAnnotationValidators.EnsureValidPageIndex(pageIndex);
        var sourceRectangles = rectangles.IsDefault
            ? Array.Empty<PdfAnnotationRect>()
            : rectangles.AsEnumerable();

        Rectangles = PdfAnnotationCollectionUtilities.NormalizeRectangles(sourceRectangles);
        SelectedText = PdfAnnotationValidators.NormalizeOptionalText(selectedText);
        Color = color;
        Notes = PdfAnnotationValidators.NormalizeOptionalText(notes);
        Tags = PdfAnnotationCollectionUtilities.NormalizeTags(tags.IsDefault ? null : tags.AsEnumerable());
        Preview = preview;
    }

    public Guid Id { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ModifiedAt { get; init; }

    public int PageIndex { get; }

    public ImmutableArray<PdfAnnotationRect> Rectangles { get; init; }

    public string? SelectedText { get; }

    public PdfAnnotationColor? Color { get; }

    public string? Notes { get; }

    public ImmutableArray<string> Tags { get; init; }

    public PdfAnnotationPreviewImage? Preview { get; }

    public PdfAnnotation WithRectangles(IEnumerable<PdfAnnotationRect> rectangles)
    {
        if (rectangles is null)
        {
            throw new ArgumentNullException(nameof(rectangles));
        }

        var normalized = PdfAnnotationCollectionUtilities.NormalizeRectangles(rectangles);
        return this with { Rectangles = normalized };
    }

    public PdfAnnotation WithTags(IEnumerable<string>? tags)
    {
        var normalizedTags = PdfAnnotationCollectionUtilities.NormalizeTags(tags);
        return this with { Tags = normalizedTags };
    }
}
