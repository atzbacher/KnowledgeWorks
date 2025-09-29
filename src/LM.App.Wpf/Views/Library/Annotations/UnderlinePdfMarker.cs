#nullable enable
using System;
using System.Drawing;
using PdfiumViewer;
using PdfiumViewer.Core;

namespace LM.App.Wpf.Views.Library.Annotations;

internal sealed class UnderlinePdfMarker : IPdfMarker
{
    private readonly RectangleF _pdfBounds;
    private readonly System.Windows.Media.Color _strokeColor;

    public UnderlinePdfMarker(int page, RectangleF pdfBounds, System.Windows.Media.Color strokeColor)
    {
        Page = page;
        _pdfBounds = pdfBounds;
        _strokeColor = strokeColor;
    }

    public int Page { get; }

    public void Draw(PdfRenderer renderer, System.Windows.Media.DrawingContext graphics)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(graphics);

        var rect = ConvertToRect(renderer.RectangleFromPdf(Page, _pdfBounds), renderer);
        var brush = new System.Windows.Media.SolidColorBrush(_strokeColor);
        brush.Freeze();
        var thickness = Math.Clamp(rect.Height * 0.15, 1.5, 4.0);
        var pen = new System.Windows.Media.Pen(brush, thickness);
        pen.Freeze();

        var start = new System.Windows.Point(rect.Left, rect.Bottom - pen.Thickness / 2);
        var end = new System.Windows.Point(rect.Right, rect.Bottom - pen.Thickness / 2);
        graphics.DrawLine(pen, start, end);
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
