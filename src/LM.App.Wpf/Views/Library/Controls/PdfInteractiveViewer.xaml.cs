#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using PdfiumViewer;
using PdfiumViewer.Core;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views.Library.Annotations;

namespace LM.App.Wpf.Views.Library.Controls;

internal sealed partial class PdfInteractiveViewer : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty SourcePathProperty = System.Windows.DependencyProperty.Register(
        nameof(SourcePath),
        typeof(string),
        typeof(PdfInteractiveViewer),
        new System.Windows.PropertyMetadata(null, OnSourcePathChanged));

    public static readonly System.Windows.DependencyProperty PageNumberProperty = System.Windows.DependencyProperty.Register(
        nameof(PageNumber),
        typeof(int?),
        typeof(PdfInteractiveViewer),
        new System.Windows.PropertyMetadata(null, OnPageNumberChanged));

    public static readonly System.Windows.DependencyProperty RoiSelectionProperty = System.Windows.DependencyProperty.Register(
        nameof(RoiSelection),
        typeof(RoiSelectionViewModel),
        typeof(PdfInteractiveViewer),
        new System.Windows.PropertyMetadata(null, OnRoiSelectionChanged));

    public static readonly System.Windows.DependencyProperty AnnotationsProperty = System.Windows.DependencyProperty.Register(
        nameof(Annotations),
        typeof(ObservableCollection<PdfAnnotationViewModel>),
        typeof(PdfInteractiveViewer),
        new System.Windows.PropertyMetadata(null, OnAnnotationsChanged));

    public static readonly System.Windows.DependencyProperty IsSelectionEnabledProperty = System.Windows.DependencyProperty.Register(
        nameof(IsSelectionEnabled),
        typeof(bool),
        typeof(PdfInteractiveViewer),
        new System.Windows.PropertyMetadata(true));

    private PdfRegionAdorner? _adorner;
    private bool _isDragging;

    public PdfInteractiveViewer()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        Renderer.PreviewMouseLeftButtonDown += OnRendererPreviewMouseLeftButtonDown;
        Renderer.PreviewMouseMove += OnRendererPreviewMouseMove;
        Renderer.PreviewMouseLeftButtonUp += OnRendererPreviewMouseLeftButtonUp;
        Renderer.MouseLeave += OnRendererMouseLeave;
        Renderer.PreviewMouseRightButtonDown += OnRendererPreviewMouseRightButtonDown;
        Renderer.PageChanged += OnRendererPageChanged;
    }

    public event EventHandler<System.Windows.Point>? RegionSelectionStarted;

    public event EventHandler<System.Windows.Point>? RegionSelectionUpdated;

    public event EventHandler<System.Windows.Point>? RegionSelectionCompleted;

    public event EventHandler? RegionSelectionCanceled;

    public event EventHandler<int>? PageChanged;

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public int? PageNumber
    {
        get => (int?)GetValue(PageNumberProperty);
        set => SetValue(PageNumberProperty, value);
    }

    internal RoiSelectionViewModel? RoiSelection
    {
        get => (RoiSelectionViewModel?)GetValue(RoiSelectionProperty);
        set => SetValue(RoiSelectionProperty, value);
    }

    internal ObservableCollection<PdfAnnotationViewModel>? Annotations
    {
        get => (ObservableCollection<PdfAnnotationViewModel>?)GetValue(AnnotationsProperty);
        set => SetValue(AnnotationsProperty, value);
    }

    public bool IsSelectionEnabled
    {
        get => (bool)GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    private static void OnSourcePathChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not PdfInteractiveViewer viewer)
        {
            return;
        }

        viewer.LoadDocument(e.NewValue as string);
    }

    private static void OnPageNumberChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not PdfInteractiveViewer viewer)
        {
            return;
        }

        if (e.NewValue is int pageIndex && pageIndex > 0)
        {
            viewer.GotoPage(pageIndex - 1);
        }
    }

    private static void OnRoiSelectionChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not PdfInteractiveViewer viewer)
        {
            return;
        }

        viewer.AttachSelectionSubscription(e.OldValue as RoiSelectionViewModel, e.NewValue as RoiSelectionViewModel);
        viewer.UpdateSelectionAdorner();
    }

    private static void OnAnnotationsChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not PdfInteractiveViewer viewer)
        {
            return;
        }

        viewer.AttachAnnotationsSubscription(e.OldValue as ObservableCollection<PdfAnnotationViewModel>, e.NewValue as ObservableCollection<PdfAnnotationViewModel>);
        viewer.RefreshMarkers();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Renderer.PageChanged -= OnRendererPageChanged;
        Renderer.PageChanged += OnRendererPageChanged;
        AttachAdorner();
        RefreshMarkers();
    }

    private void OnUnloaded(object? sender, System.Windows.RoutedEventArgs e)
    {
        Renderer.PageChanged -= OnRendererPageChanged;
        Renderer.UnLoad();
    }

    public void ZoomIn()
    {
        Renderer.ZoomIn();
    }

    public void ZoomOut()
    {
        Renderer.ZoomOut();
    }

    public void SetZoom(double zoom)
    {
        Renderer.SetZoom(zoom);
    }

    public void SetZoomMode(PdfiumViewer.Enums.PdfViewerZoomMode mode)
    {
        Renderer.SetZoomMode(mode);
    }

    public void RotateClockwise()
    {
        Renderer.ClockwiseRotate();
    }

    public void RotateCounterClockwise()
    {
        Renderer.Counterclockwise();
    }

    public bool TryNavigateToPage(int pageNumber)
    {
        if (pageNumber <= 0 || Renderer.Document is null)
        {
            return false;
        }

        var zeroBased = pageNumber - 1;
        if (zeroBased >= Renderer.Document.PageCount)
        {
            return false;
        }

        Renderer.GotoPage(zeroBased);
        return true;
    }

    public System.Collections.IList? Search(string text, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrWhiteSpace(text) || Renderer.Document is null)
        {
            return null;
        }

        var result = Renderer.Search(text, matchCase, wholeWord, 0, Renderer.Document.PageCount - 1);
        if (result is null)
        {
            return null;
        }

        return result.Items as System.Collections.IList;
    }

    public void FocusViewer()
    {
        Renderer.Focus();
    }

    public int GetPageCount()
    {
        return Renderer.Document?.PageCount ?? 0;
    }

    public void ScrollIntoView(object match)
    {
        if (match is null)
        {
            return;
        }

        dynamic dynMatch = match;
        Renderer.GotoPage((int)dynMatch.Page);

        var bounds = Renderer.GetTextBounds(dynMatch.TextSpan) as System.Collections.IList;
        if (bounds is null || bounds.Count == 0)
        {
            return;
        }

        var rectangle = bounds[0];
        if (rectangle is null)
        {
            return;
        }

        Renderer.ScrollIntoView((dynamic)rectangle);
    }

    private void OnRendererPageChanged(object? sender, int pageIndex)
    {
        SetCurrentValue(PageNumberProperty, pageIndex + 1);
        PageChanged?.Invoke(this, pageIndex + 1);
    }

    private void AttachAdorner()
    {
        if (_adorner is not null)
        {
            return;
        }

        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(Renderer);
        if (layer is null)
        {
            return;
        }

        _adorner = new PdfRegionAdorner(Renderer);
        layer.Add(_adorner);
        UpdateSelectionAdorner();
    }

    private void LoadDocument(string? path)
    {
        Renderer.Markers.Clear();
        if (string.IsNullOrWhiteSpace(path))
        {
            Renderer.UnLoad();
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                System.Windows.MessageBox.Show(
                    $"PDF file could not be found at {path}.",
                    "PDF preview",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            Renderer.OpenPdf(path);
            if (PageNumber.HasValue && PageNumber.Value > 0)
            {
                GotoPage(PageNumber.Value - 1);
            }
            RefreshMarkers();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to load PDF: {ex.Message}",
                "PDF preview",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void GotoPage(int pageIndex)
    {
        if (pageIndex < 0)
        {
            return;
        }

        try
        {
            Renderer.GotoPage(pageIndex);
        }
        catch
        {
            // ignore page navigation failures
        }
    }

    private void AttachSelectionSubscription(RoiSelectionViewModel? oldValue, RoiSelectionViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnRoiPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnRoiPropertyChanged;
        }
    }

    private void AttachAnnotationsSubscription(ObservableCollection<PdfAnnotationViewModel>? oldValue,
                                                ObservableCollection<PdfAnnotationViewModel>? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.CollectionChanged -= OnAnnotationsCollectionChanged;
        }

        if (newValue is not null)
        {
            newValue.CollectionChanged += OnAnnotationsCollectionChanged;
        }
    }

    private void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshMarkers();
    }

    private void OnRoiPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoiSelectionViewModel.HasSelection)
            or nameof(RoiSelectionViewModel.IsSelecting)
            or nameof(RoiSelectionViewModel.X)
            or nameof(RoiSelectionViewModel.Y)
            or nameof(RoiSelectionViewModel.Width)
            or nameof(RoiSelectionViewModel.Height))
        {
            UpdateSelectionAdorner();
        }
    }

    private void UpdateSelectionAdorner()
    {
        if (_adorner is null || RoiSelection is null)
        {
            return;
        }

        var rect = RoiSelection.HasVisibleSelection
            ? new System.Windows.Rect(RoiSelection.X, RoiSelection.Y, RoiSelection.Width, RoiSelection.Height)
            : (System.Windows.Rect?)null;
        _adorner.Update(rect);
    }

    private void RefreshMarkers()
    {
        Renderer.Markers.Clear();

        if (Annotations is null)
        {
            return;
        }

        foreach (var annotation in Annotations)
        {
            switch (annotation.Kind)
            {
                case PdfAnnotationKind.Highlight:
                    Renderer.Markers.Add(new HighlightPdfMarker(
                        annotation.PageNumber - 1,
                        annotation.PdfBounds,
                        System.Windows.Media.Color.FromArgb(168, 255, 241, 118)));
                    break;
                case PdfAnnotationKind.Note:
                    Renderer.Markers.Add(new NotePdfMarker(
                        annotation.PageNumber - 1,
                        annotation.PdfBounds,
                        annotation.Note ?? string.Empty,
                        System.Windows.Media.Color.FromArgb(192, 255, 249, 196),
                        System.Windows.Media.Color.FromRgb(102, 124, 64)));
                    break;
            }
        }
    }

    private void OnRendererPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsSelectionEnabled)
        {
            return;
        }

        Renderer.Focus();
        CaptureMouse();
        _isDragging = true;
        var position = e.GetPosition(Renderer);
        RegionSelectionStarted?.Invoke(this, position);
        e.Handled = true;
    }

    private void OnRendererPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetPosition(Renderer);
        RegionSelectionUpdated?.Invoke(this, position);
    }

    private void OnRendererPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        ReleaseMouseCapture();
        _isDragging = false;
        var position = e.GetPosition(Renderer);
        RegionSelectionCompleted?.Invoke(this, position);
        e.Handled = true;
    }

    private void OnRendererMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        ReleaseMouseCapture();
        _isDragging = false;
        var position = e.GetPosition(Renderer);
        RegionSelectionCompleted?.Invoke(this, position);
    }

    private void OnRendererPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            RegionSelectionCanceled?.Invoke(this, EventArgs.Empty);
            return;
        }

        ReleaseMouseCapture();
        _isDragging = false;
        RegionSelectionCanceled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
