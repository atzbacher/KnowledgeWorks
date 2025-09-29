using System;
using System.Collections.Immutable;

namespace LM.Core.Models.Pdf;

internal interface IPdfAnnotation
{
    Guid Id { get; }

    DateTimeOffset CreatedAt { get; }

    DateTimeOffset ModifiedAt { get; }

    int PageIndex { get; }

    ImmutableArray<PdfAnnotationRect> Rectangles { get; }

    string? SelectedText { get; }

    PdfAnnotationColor? Color { get; }

    string? Notes { get; }

    ImmutableArray<string> Tags { get; }

    PdfAnnotationPreviewImage? Preview { get; }
}
