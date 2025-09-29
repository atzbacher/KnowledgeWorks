using System;

namespace LM.Core.Models.Pdf;

internal interface IPdfAnnotationOverlay
{
    Guid AnnotationId { get; }

    int PageIndex { get; }

    PdfAnnotationRect Rect { get; }

    PdfAnnotationColor? Color { get; }

    string? Label { get; }
}
