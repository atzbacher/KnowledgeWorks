#nullable enable
using System;
using CommunityToolkit.Mvvm.Input;
using PdfiumViewer.Enums;

namespace LM.App.Wpf.ViewModels.Library;

public sealed partial class PdfViewerViewModel
{
    [RelayCommand]
    private void ToggleSidePane()
    {
        IsSidePaneVisible = !IsSidePaneVisible;
    }

    [RelayCommand]
    private void ShowPageList()
    {
        SelectedPaneTab = PdfViewerPaneTab.Thumbnails;
        if (!IsSidePaneVisible)
        {
            IsSidePaneVisible = true;
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        _surface?.ZoomIn();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _surface?.ZoomOut();
    }

    [RelayCommand]
    private void FitWidth()
    {
        _surface?.SetZoomMode(PdfViewerZoomMode.FitWidth);
    }

    [RelayCommand]
    private void FitHeight()
    {
        _surface?.SetZoomMode(PdfViewerZoomMode.FitHeight);
    }

    [RelayCommand]
    private void ActualSize()
    {
        _surface?.SetZoomMode(PdfViewerZoomMode.None);
        _surface?.SetZoom(1.0);
    }

    [RelayCommand]
    private void RotateClockwise()
    {
        _surface?.RotateClockwise();
    }

    [RelayCommand]
    private void RotateCounterClockwise()
    {
        _surface?.RotateCounterClockwise();
    }

    [RelayCommand]
    private void FindNext()
    {
        NavigateSearch(true);
    }

    [RelayCommand]
    private void FindPrevious()
    {
        NavigateSearch(false);
    }

    [RelayCommand]
    private void FocusViewer()
    {
        _surface?.FocusViewer();
    }

    [RelayCommand]
    private void ActivateHighlight()
    {
        ActiveAnnotationTool = PdfAnnotationTool.Highlight;
    }

    [RelayCommand]
    private void ActivateUnderline()
    {
        ActiveAnnotationTool = PdfAnnotationTool.Underline;
    }

    [RelayCommand]
    private void ActivateRectangle()
    {
        ActiveAnnotationTool = PdfAnnotationTool.Rectangle;
    }

    [RelayCommand]
    private void ActivateNote()
    {
        ActiveAnnotationTool = PdfAnnotationTool.Note;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        ResetSearchState();
    }

    [RelayCommand]
    private void BeginSearch()
    {
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }
}
