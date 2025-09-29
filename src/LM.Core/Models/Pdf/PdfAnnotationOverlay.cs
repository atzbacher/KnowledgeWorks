using System;
using System.Collections.Immutable;
using System.Linq;

namespace LM.Core.Models.Pdf;

public sealed record PdfAnnotationOverlay : IPdfAnnotationOverlay
{
    public PdfAnnotationOverlay(Guid annotationId, int pageIndex, PdfAnnotationRect rect, PdfAnnotationColor? color, string? label = null)
    {
        AnnotationId = PdfAnnotationValidators.EnsureValidId(annotationId);
        PageIndex = PdfAnnotationValidators.EnsureValidPageIndex(pageIndex);
        Rect = rect ?? throw new ArgumentNullException(nameof(rect));
        Color = color;
        Label = PdfAnnotationValidators.NormalizeOptionalText(label);
    }

    public Guid AnnotationId { get; }

    public int PageIndex { get; }

    public PdfAnnotationRect Rect { get; }

    public PdfAnnotationColor? Color { get; }

    public string? Label { get; }

    public static ImmutableArray<PdfAnnotationOverlay> FromAnnotation(IPdfAnnotation annotation, Func<PdfAnnotationRect, string?>? labelSelector = null)
    {
        if (annotation is null)
        {
            throw new ArgumentNullException(nameof(annotation));
        }

        if (annotation.Rectangles.Length == 0)
        {
            return ImmutableArray<PdfAnnotationOverlay>.Empty;
        }

        var overlays = annotation.Rectangles
            .Select(rect => new PdfAnnotationOverlay(annotation.Id, annotation.PageIndex, rect, annotation.Color, labelSelector?.Invoke(rect)))
            .ToImmutableArray();

        return overlays.IsDefault ? ImmutableArray<PdfAnnotationOverlay>.Empty : overlays;
    }
}
