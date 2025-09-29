#nullable enable
using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed class AnnotationCaptureViewModel : ObservableObject
{
    public AnnotationCaptureViewModel(Guid annotationId,
                                      PdfAnnotationKind kind,
                                      int pageNumber,
                                      System.Windows.Media.Color color,
                                      DateTime createdAtUtc,
                                      System.Windows.Media.Imaging.BitmapSource? thumbnail,
                                      string? text)
    {
        AnnotationId = annotationId;
        Kind = kind;
        PageNumber = pageNumber;
        Color = color;
        ColorBrush = CreateBrush(color);
        CreatedAtUtc = createdAtUtc;
        Thumbnail = thumbnail;
        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        TimestampDisplay = createdAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        Title = BuildTitle(kind, pageNumber);
    }

    public Guid AnnotationId { get; }

    public PdfAnnotationKind Kind { get; }

    public int PageNumber { get; }

    public System.Windows.Media.Color Color { get; }

    public System.Windows.Media.Brush ColorBrush { get; }

    public DateTime CreatedAtUtc { get; }

    public System.Windows.Media.Imaging.BitmapSource? Thumbnail { get; }

    public string? Text { get; }

    public string Title { get; }

    public string TimestampDisplay { get; }

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    private static System.Windows.Media.Brush CreateBrush(System.Windows.Media.Color color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string BuildTitle(PdfAnnotationKind kind, int pageNumber)
    {
        return kind switch
        {
            PdfAnnotationKind.Highlight => $"Highlight • Page {pageNumber}",
            PdfAnnotationKind.Underline => $"Underline • Page {pageNumber}",
            PdfAnnotationKind.Note => $"Note • Page {pageNumber}",
            _ => $"Annotation • Page {pageNumber}"
        };
    }
}
