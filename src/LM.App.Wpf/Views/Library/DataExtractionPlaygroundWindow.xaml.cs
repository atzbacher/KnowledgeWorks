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
        }

        private void OnRoiCanvasMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var position = e.GetPosition(RoiCanvas);
            _viewModel.BeginRegionSelection(position);

            if (_viewModel.RoiSelection.IsSelecting)
            {
                _isDragging = true;
                RoiCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnRoiCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var position = e.GetPosition(RoiCanvas);
            _viewModel.UpdateRegionSelection(position);
        }

        private void OnRoiCanvasMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var position = e.GetPosition(RoiCanvas);
            _viewModel.CompleteRegionSelection(position);
            RoiCanvas.ReleaseMouseCapture();
            _isDragging = false;
            e.Handled = true;
        }

        private void OnRoiCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var position = e.GetPosition(RoiCanvas);
            _viewModel.CompleteRegionSelection(position);
            RoiCanvas.ReleaseMouseCapture();
            _isDragging = false;
        }

        private void OnRoiCanvasMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _viewModel.CancelRegionSelection();
            RoiCanvas.ReleaseMouseCapture();
            _isDragging = false;
            e.Handled = true;
        }
    }
}
