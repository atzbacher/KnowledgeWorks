#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.Views.Library
{
    internal sealed partial class PdfViewerWindow : Window
    {
        private PdfViewerSurfaceAdapter? _surfaceAdapter;

        public PdfViewerWindow()
        {
            InitializeComponent();
            OutlineTree.SelectedItemChanged += OnOutlineTreeSelectedItemChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PdfViewerViewModel oldViewModel)
            {
                oldViewModel.SearchRequested -= OnSearchRequested;
                oldViewModel.DetachSurface();
            }

            if (e.NewValue is PdfViewerViewModel viewModel)
            {
                EnsureSurfaceAdapter();
                viewModel.AttachSurface(_surfaceAdapter!);
                viewModel.SearchRequested += OnSearchRequested;
            }
        }

        private void EnsureSurfaceAdapter()
        {
            if (_surfaceAdapter is not null)
            {
                return;
            }

            _surfaceAdapter = new PdfViewerSurfaceAdapter(ViewerControl);
        }

        private void OnSearchRequested(object? sender, EventArgs e)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void OnOutlineTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not PdfViewerViewModel viewModel)
            {
                return;
            }

            if (e.NewValue is PdfOutlineNodeViewModel node)
            {
                viewModel.SelectedOutline = node;
            }
        }

        private sealed class PdfViewerSurfaceAdapter : IPdfViewerSurface
        {
            private readonly Controls.PdfInteractiveViewer _viewer;

            public PdfViewerSurfaceAdapter(Controls.PdfInteractiveViewer viewer)
            {
                _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
                _viewer.PageChanged += OnViewerPageChanged;
            }

            public event EventHandler<int>? PageChanged;

            public int PageCount => _viewer.GetPageCount();

            public void ZoomIn() => _viewer.ZoomIn();

            public void ZoomOut() => _viewer.ZoomOut();

            public void SetZoom(double zoom) => _viewer.SetZoom(zoom);

            public void SetZoomMode(PdfiumViewer.Enums.PdfViewerZoomMode mode) => _viewer.SetZoomMode(mode);

            public void RotateClockwise() => _viewer.RotateClockwise();

            public void RotateCounterClockwise() => _viewer.RotateCounterClockwise();

            public bool TryNavigateToPage(int pageNumber) => _viewer.TryNavigateToPage(pageNumber);

            public System.Collections.IList? Search(string text, bool matchCase, bool wholeWord) => _viewer.Search(text, matchCase, wholeWord);

            public void ScrollIntoView(object match) => _viewer.ScrollIntoView(match);

            public void FocusViewer() => _viewer.FocusViewer();

            private void OnViewerPageChanged(object? sender, int e)
            {
                PageChanged?.Invoke(this, e);
            }
        }
    }
}
