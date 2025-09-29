using System;
using System.Collections;

namespace LM.App.Wpf.Views.Library.Controls
{
    internal partial class MuPdfPageSurface : System.Windows.Controls.UserControl
    {
        public static readonly System.Windows.DependencyProperty ImageSourceProperty = System.Windows.DependencyProperty.Register(
            nameof(ImageSource),
            typeof(System.Windows.Media.ImageSource),
            typeof(MuPdfPageSurface),
            new System.Windows.PropertyMetadata(null, OnImageSourceChanged));

        public static readonly System.Windows.DependencyProperty AnnotationsProperty = System.Windows.DependencyProperty.Register(
            nameof(Annotations),
            typeof(IEnumerable),
            typeof(MuPdfPageSurface),
            new System.Windows.PropertyMetadata(null));

        public static readonly System.Windows.DependencyProperty IsSelectionEnabledProperty = System.Windows.DependencyProperty.Register(
            nameof(IsSelectionEnabled),
            typeof(bool),
            typeof(MuPdfPageSurface),
            new System.Windows.PropertyMetadata(true));

        public static readonly System.Windows.DependencyProperty SelectionCommandProperty = System.Windows.DependencyProperty.Register(
            nameof(SelectionCommand),
            typeof(System.Windows.Input.ICommand),
            typeof(MuPdfPageSurface),
            new System.Windows.PropertyMetadata(null));

        private System.Windows.Point? _dragStart;

        public MuPdfPageSurface()
        {
            InitializeComponent();
            PageImage.SizeChanged += HandleImageSizeChanged;
        }

        public System.Windows.Media.ImageSource? ImageSource
        {
            get => (System.Windows.Media.ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public IEnumerable? Annotations
        {
            get => (IEnumerable?)GetValue(AnnotationsProperty);
            set => SetValue(AnnotationsProperty, value);
        }

        public bool IsSelectionEnabled
        {
            get => (bool)GetValue(IsSelectionEnabledProperty);
            set => SetValue(IsSelectionEnabledProperty, value);
        }

        public System.Windows.Input.ICommand? SelectionCommand
        {
            get => (System.Windows.Input.ICommand?)GetValue(SelectionCommandProperty);
            set => SetValue(SelectionCommandProperty, value);
        }

        private static void OnImageSourceChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is MuPdfPageSurface surface)
            {
                surface.UpdateCanvasSize();
            }
        }

        private void HandleImageSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            OverlayCanvas.Width = PageImage.ActualWidth;
            OverlayCanvas.Height = PageImage.ActualHeight;
            SelectionVisual.Width = 0d;
            SelectionVisual.Height = 0d;
            SelectionVisual.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void HandleMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!IsSelectionEnabled)
            {
                return;
            }

            var position = e.GetPosition(OverlayCanvas);
            _dragStart = position;
            System.Windows.Controls.Canvas.SetLeft(SelectionVisual, position.X);
            System.Windows.Controls.Canvas.SetTop(SelectionVisual, position.Y);
            SelectionVisual.Width = 0d;
            SelectionVisual.Height = 0d;
            SelectionVisual.Visibility = System.Windows.Visibility.Visible;
            OverlayCanvas.CaptureMouse();
        }

        private void HandleMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStart is null)
            {
                return;
            }

            var current = e.GetPosition(OverlayCanvas);
            DrawSelectionRectangle(_dragStart.Value, current);
        }

        private void HandleMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_dragStart is null)
            {
                return;
            }

            OverlayCanvas.ReleaseMouseCapture();
            var start = _dragStart.Value;
            var end = e.GetPosition(OverlayCanvas);
            _dragStart = null;

            var rect = NormalizeRect(start, end);
            SelectionVisual.Visibility = System.Windows.Visibility.Collapsed;

            if (rect.Width < 4d || rect.Height < 4d)
            {
                return;
            }

            if (SelectionCommand is { } command && command.CanExecute(rect))
            {
                command.Execute(rect);
            }
        }

        private void HandleMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!OverlayCanvas.IsMouseCaptured)
            {
                SelectionVisual.Visibility = System.Windows.Visibility.Collapsed;
                _dragStart = null;
            }
        }

        private void DrawSelectionRectangle(System.Windows.Point start, System.Windows.Point end)
        {
            var rect = NormalizeRect(start, end);
            System.Windows.Controls.Canvas.SetLeft(SelectionVisual, rect.X);
            System.Windows.Controls.Canvas.SetTop(SelectionVisual, rect.Y);
            SelectionVisual.Width = rect.Width;
            SelectionVisual.Height = rect.Height;
        }

        private static System.Windows.Rect NormalizeRect(System.Windows.Point start, System.Windows.Point end)
        {
            var x = Math.Min(start.X, end.X);
            var y = Math.Min(start.Y, end.Y);
            var width = Math.Abs(end.X - start.X);
            var height = Math.Abs(end.Y - start.Y);
            return new System.Windows.Rect(x, y, width, height);
        }
    }
}
