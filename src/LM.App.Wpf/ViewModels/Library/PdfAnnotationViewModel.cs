#nullable enable
using System;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

internal enum PdfAnnotationKind
{
    Highlight,
    Note,
}

internal sealed partial class PdfAnnotationViewModel : ObservableObject
{
    public PdfAnnotationViewModel(PdfAnnotationKind kind,
                                  int pageNumber,
                                  RectangleF pdfBounds,
                                  string? note,
                                  DateTime createdAt)
    {
        Kind = kind;
        PageNumber = pageNumber;
        PdfBounds = pdfBounds;
        Note = note;
        CreatedAt = createdAt;
        AnnotationId = Guid.NewGuid();
    }

    public Guid AnnotationId { get; }

    public PdfAnnotationKind Kind { get; }

    public int PageNumber { get; }

    public RectangleF PdfBounds { get; }

    public string? Note { get; }

    public DateTime CreatedAt { get; }
}
