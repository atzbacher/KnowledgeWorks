#nullable enable
using System;
using System.Drawing;
using PdfiumViewer;
using PdfiumViewer.Core;

namespace LM.App.Wpf.Views.Library.Annotations;

internal sealed class HighlightPdfMarker : IPdfMarker
{
    private readonly RectangleF _pdfBounds;
    private readonly System.Windows.Media.Color _fillColor;

    public HighlightPdfMarker(int page,
                               RectangleF pdfBounds,
                               System.Windows.Media.Color fillColor)
    {
        Page = page;
        _pdfBounds = pdfBounds;
        _fillColor = fillColor;
    }

    public int Page { get; }

    public void Draw(PdfRenderer renderer, System.Windows.Media.DrawingContext graphics)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(graphics);

        var rect = ConvertToRect(renderer.RectangleFromPdf(Page, _pdfBounds), renderer);
        var brush = new System.Windows.Media.SolidColorBrush(_fillColor)
        {
            Opacity = 0.6,
        };
        brush.Freeze();

        graphics.DrawRectangle(brush, null, rect);
    }

    private static System.Windows.Rect ConvertToRect(System.Drawing.Rectangle rectangle, System.Windows.Media.Visual visual)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(visual);
        var x = rectangle.X / dpi.DpiScaleX;
        var y = rectangle.Y / dpi.DpiScaleY;
        var width = rectangle.Width / dpi.DpiScaleX;
        var height = rectangle.Height / dpi.DpiScaleY;
        return new System.Windows.Rect(x, y, width, height);
    }
}
