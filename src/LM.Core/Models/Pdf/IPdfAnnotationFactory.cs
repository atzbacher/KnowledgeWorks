using System;
using System.Collections.Generic;

namespace LM.Core.Models.Pdf;

public interface IPdfAnnotationFactory
{
    PdfAnnotation Create(
        Guid id,
        DateTimeOffset createdAt,
        int pageIndex,
        IEnumerable<PdfAnnotationRect> rectangles,
        string? selectedText = null,
        PdfAnnotationColor? color = null,
        string? notes = null,
        IEnumerable<string>? tags = null,
        PdfAnnotationPreviewImage? preview = null,
        DateTimeOffset? modifiedAt = null);

    PdfAnnotation UpdateRectangles(PdfAnnotation annotation, IEnumerable<PdfAnnotationRect> rectangles);

    PdfAnnotation UpdateTags(PdfAnnotation annotation, IEnumerable<string>? tags);
}
