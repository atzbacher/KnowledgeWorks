#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels.Library;

internal static class PdfAnnotationViewModelFactory
{
    public static PdfAnnotationViewModel Create(LibraryAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        var geometry = annotation.Geometry;
        var bounds = new RectangleF(geometry.X, geometry.Y, geometry.Width, geometry.Height);

        return new PdfAnnotationViewModel(
            MapKind(annotation.AnnotationType),
            annotation.PageNumber,
            bounds,
            annotation.Note,
            annotation.CreatedAtUtc,
            annotation.AnnotationId,
            annotation.Title,
            annotation.Tags,
            annotation.ColorKey,
            null,
            annotation.Meaning,
            annotation.CreatedBy,
            annotation.LastModifiedUtc);
    }

    public static IReadOnlyList<PdfAnnotationViewModel> CreateMany(IEnumerable<LibraryAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        return annotations.Select(Create).ToList();
    }

    private static PdfAnnotationKind MapKind(LibraryAnnotationType annotationType)
    {
        return annotationType switch
        {
            LibraryAnnotationType.Highlight => PdfAnnotationKind.Highlight,
            LibraryAnnotationType.Note => PdfAnnotationKind.Note,
            LibraryAnnotationType.Rectangle => PdfAnnotationKind.Rectangle,
            LibraryAnnotationType.Underline => PdfAnnotationKind.Underline,
            _ => PdfAnnotationKind.Highlight
        };
    }
}
