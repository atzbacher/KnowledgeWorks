#nullable enable
using System;
using System.Collections;
using PdfiumViewer.Enums;

namespace LM.App.Wpf.ViewModels.Library;

public interface IPdfViewerSurface
{
    event EventHandler<int>? PageChanged;

    int PageCount { get; }

    void ZoomIn();

    void ZoomOut();

    void SetZoom(double zoom);

    void SetZoomMode(PdfViewerZoomMode mode);

    void RotateClockwise();

    void RotateCounterClockwise();

    bool TryNavigateToPage(int pageNumber);

    IList? Search(string text, bool matchCase, bool wholeWord);

    void ScrollIntoView(object match);

    void FocusViewer();
}
