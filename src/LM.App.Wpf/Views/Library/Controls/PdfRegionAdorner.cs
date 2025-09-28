#nullable enable

namespace LM.App.Wpf.Views.Library.Controls;

internal sealed class PdfRegionAdorner : System.Windows.Documents.Adorner
{
    private System.Windows.Rect? _selection;

    public PdfRegionAdorner(System.Windows.UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public void Update(System.Windows.Rect? selection)
    {
        _selection = selection;
        InvalidateVisual();
    }

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_selection is null)
        {
            return;
        }

        var rect = _selection.Value;
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 30, 136, 229));
        var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 136, 229)), 2);
        pen.Freeze();
        drawingContext.DrawRectangle(brush, pen, rect);
    }
}
