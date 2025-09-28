#nullable enable
using System;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.Views.Library
{
    internal partial class DataExtractionPlaygroundWindow : System.Windows.Window
    {
        private readonly DataExtractionPlaygroundViewModel _viewModel;
        private bool _isDragging;

        internal DataExtractionPlaygroundWindow(DataExtractionPlaygroundViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            PdfViewer.RegionSelectionStarted += OnRegionSelectionStarted;
            PdfViewer.RegionSelectionUpdated += OnRegionSelectionUpdated;
            PdfViewer.RegionSelectionCompleted += OnRegionSelectionCompleted;
            PdfViewer.RegionSelectionCanceled += OnRegionSelectionCanceled;
        }

        private void OnRegionSelectionStarted(object? sender, System.Windows.Point position)
        {
            _viewModel.BeginRegionSelection(position);
            _isDragging = _viewModel.RoiSelection.IsSelecting;
        }

        private void OnRegionSelectionUpdated(object? sender, System.Windows.Point position)
        {
            if (!_isDragging)
            {
                return;
            }

            _viewModel.UpdateRegionSelection(position);
        }

        private void OnRegionSelectionCompleted(object? sender, System.Windows.Point position)
        {
            if (!_isDragging)
            {
                return;
            }

            _viewModel.CompleteRegionSelection(position);
            _isDragging = false;
        }

        private void OnRegionSelectionCanceled(object? sender, EventArgs e)
        {
            _viewModel.CancelRegionSelection();
            _isDragging = false;
        }
    }
}
