#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace LM.App.Wpf.Views.Controls
{
    internal partial class PdfDocumentView : System.Windows.Controls.UserControl
    {
        private const float BaseRenderScale = 1.5f;

        public static readonly System.Windows.DependencyProperty DocumentPathProperty =
            System.Windows.DependencyProperty.Register(
                nameof(DocumentPath),
                typeof(string),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(string.Empty, OnDocumentPathChanged));

        public static readonly System.Windows.DependencyProperty PageNumberProperty =
            System.Windows.DependencyProperty.Register(
                nameof(PageNumber),
                typeof(int),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(1, OnPageNumberChanged));

        public static readonly System.Windows.DependencyProperty PageCountProperty =
            System.Windows.DependencyProperty.Register(
                nameof(PageCount),
                typeof(int),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(0));

        public static readonly System.Windows.DependencyProperty ZoomProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Zoom),
                typeof(double),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(1d, OnZoomChanged));

        public static readonly System.Windows.DependencyProperty RegionsProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Regions),
                typeof(IEnumerable<DataExtractionRegionViewModel>),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(null, OnRegionsChanged));

        public static readonly System.Windows.DependencyProperty SelectedRegionProperty =
            System.Windows.DependencyProperty.Register(
                nameof(SelectedRegion),
                typeof(DataExtractionRegionViewModel),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(null, OnSelectedRegionChanged));

        public static readonly System.Windows.DependencyProperty IsCreateRegionModeProperty =
            System.Windows.DependencyProperty.Register(
                nameof(IsCreateRegionMode),
                typeof(bool),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(false, OnIsCreateRegionModeChanged));

        public static readonly System.Windows.DependencyProperty CreateRegionCommandProperty =
            System.Windows.DependencyProperty.Register(
                nameof(CreateRegionCommand),
                typeof(ICommand),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(null));

        public static readonly System.Windows.DependencyProperty UpdateRegionCommandProperty =
            System.Windows.DependencyProperty.Register(
                nameof(UpdateRegionCommand),
                typeof(ICommand),
                typeof(PdfDocumentView),
                new System.Windows.PropertyMetadata(null));

        private readonly Dictionary<DataExtractionRegionViewModel, RegionVisual> _regionVisuals = new();
        private readonly Dictionary<DataExtractionRegionViewModel, PropertyChangedEventHandler> _regionHandlers = new();
        private INotifyCollectionChanged? _regionCollection;
        private PdfDocument? _document;
        private double _pageWidth;
        private double _pageHeight;
        private bool _isDrawing;
        private System.Windows.Point _drawStart;
        private System.Windows.Shapes.Rectangle? _drawRectangle;

        public PdfDocumentView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            PART_Overlay.MouseLeftButtonDown += OnOverlayMouseLeftButtonDown;
            PART_Overlay.MouseMove += OnOverlayMouseMove;
            PART_Overlay.MouseLeftButtonUp += OnOverlayMouseLeftButtonUp;
        }

        public string DocumentPath
        {
            get => (string)GetValue(DocumentPathProperty);
            set => SetValue(DocumentPathProperty, value);
        }

        public int PageNumber
        {
            get => (int)GetValue(PageNumberProperty);
            set => SetValue(PageNumberProperty, value);
        }

        public int PageCount
        {
            get => (int)GetValue(PageCountProperty);
            set => SetValue(PageCountProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public IEnumerable<DataExtractionRegionViewModel>? Regions
        {
            get => (IEnumerable<DataExtractionRegionViewModel>?)GetValue(RegionsProperty);
            set => SetValue(RegionsProperty, value);
        }

        public DataExtractionRegionViewModel? SelectedRegion
        {
            get => (DataExtractionRegionViewModel?)GetValue(SelectedRegionProperty);
            set => SetValue(SelectedRegionProperty, value);
        }

        public bool IsCreateRegionMode
        {
            get => (bool)GetValue(IsCreateRegionModeProperty);
            set => SetValue(IsCreateRegionModeProperty, value);
        }

        public ICommand? CreateRegionCommand
        {
            get => (ICommand?)GetValue(CreateRegionCommandProperty);
            set => SetValue(CreateRegionCommandProperty, value);
        }

        public ICommand? UpdateRegionCommand
        {
            get => (ICommand?)GetValue(UpdateRegionCommandProperty);
            set => SetValue(UpdateRegionCommandProperty, value);
        }

        private static void OnDocumentPathChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.LoadDocument();
            }
        }

        private static void OnPageNumberChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.RenderCurrentPage();
            }
        }

        private static void OnZoomChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.RenderCurrentPage();
            }
        }

        private static void OnRegionsChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.AttachRegions(e.OldValue as IEnumerable<DataExtractionRegionViewModel>, e.NewValue as IEnumerable<DataExtractionRegionViewModel>);
            }
        }

        private static void OnSelectedRegionChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.UpdateHighlightStates();
            }
        }

        private static void OnIsCreateRegionModeChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfDocumentView view)
            {
                view.UpdateOverlayCursor();
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateOverlayCursor();
            RenderCurrentPage();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DisposeDocument();
        }

        private void LoadDocument()
        {
            DisposeDocument();

            if (string.IsNullOrWhiteSpace(DocumentPath) || !File.Exists(DocumentPath))
            {
                SetCurrentValue(PageCountProperty, 0);
                ClearPage();
                return;
            }

            try
            {
                _document = PdfDocument.Open(DocumentPath);
                _document.AddSkiaPageFactory();
                SetCurrentValue(PageCountProperty, _document.NumberOfPages);
                if (PageNumber < 1)
                {
                    SetCurrentValue(PageNumberProperty, 1);
                }
                RenderCurrentPage();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(FormattableString.Invariant($"[PdfDocumentView] Failed to load '{DocumentPath}': {ex.Message}"));
                DisposeDocument();
                SetCurrentValue(PageCountProperty, 0);
                ClearPage();
            }
        }

        private void RenderCurrentPage()
        {
            if (!IsLoaded)
                return;

            if (_document is null)
            {
                ClearPage();
                return;
            }

            var pageNumber = PageNumber;
            if (pageNumber < 1)
            {
                pageNumber = 1;
                SetCurrentValue(PageNumberProperty, pageNumber);
            }

            if (pageNumber > _document.NumberOfPages)
            {
                pageNumber = _document.NumberOfPages;
                SetCurrentValue(PageNumberProperty, pageNumber);
            }

            try
            {
                var scale = (float)Math.Clamp(Zoom, 0.5d, 4d) * BaseRenderScale;
                using var bitmap = _document.GetPageAsSKBitmap(pageNumber, scale);
                _pageWidth = bitmap.Width;
                _pageHeight = bitmap.Height;
                var source = ToBitmapSource(bitmap);
                source.Freeze();

                PART_Image.Source = source;
                PART_Image.Width = _pageWidth;
                PART_Image.Height = _pageHeight;
                UpdateOverlaySize();
                UpdateAllRegionVisuals();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(FormattableString.Invariant($"[PdfDocumentView] Failed to render page {pageNumber}: {ex.Message}"));
                ClearPage();
            }
        }

        private void ClearPage()
        {
            PART_Image.Source = null;
            _pageWidth = 0;
            _pageHeight = 0;
            UpdateOverlaySize();
            UpdateAllRegionVisuals();
        }

        private static System.Windows.Media.Imaging.BitmapImage ToBitmapSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using var stream = new MemoryStream(data.ToArray());
            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        private void AttachRegions(IEnumerable<DataExtractionRegionViewModel>? oldRegions,
                                   IEnumerable<DataExtractionRegionViewModel>? newRegions)
        {
            if (_regionCollection is not null)
            {
                _regionCollection.CollectionChanged -= OnRegionCollectionChanged;
                _regionCollection = null;
            }

            foreach (var handler in _regionHandlers)
            {
                handler.Key.PropertyChanged -= handler.Value;
            }

            _regionHandlers.Clear();
            _regionVisuals.Clear();
            PART_Overlay.Children.Clear();

            if (newRegions is null)
                return;

            if (newRegions is INotifyCollectionChanged notify)
            {
                _regionCollection = notify;
                notify.CollectionChanged += OnRegionCollectionChanged;
            }

            foreach (var region in newRegions)
            {
                AddRegionVisual(region);
            }

            UpdateAllRegionVisuals();
            UpdateHighlightStates();
        }

        private void OnRegionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                AttachRegions(null, Regions);
                return;
            }

            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DataExtractionRegionViewModel region)
                    {
                        RemoveRegionVisual(region);
                    }
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DataExtractionRegionViewModel region)
                    {
                        AddRegionVisual(region);
                    }
                }
            }

            UpdateAllRegionVisuals();
            UpdateHighlightStates();
        }

        private void AddRegionVisual(DataExtractionRegionViewModel region)
        {
            if (_regionVisuals.ContainsKey(region))
                return;

            var visual = new RegionVisual(this, region);
            _regionVisuals[region] = visual;
            PART_Overlay.Children.Add(visual.Root);

            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName is nameof(DataExtractionRegionViewModel.X) or
                    nameof(DataExtractionRegionViewModel.Y) or
                    nameof(DataExtractionRegionViewModel.Width) or
                    nameof(DataExtractionRegionViewModel.Height) or
                    nameof(DataExtractionRegionViewModel.PageNumber))
                {
                    UpdateRegionVisual(region);
                }
            };

            region.PropertyChanged += handler;
            _regionHandlers[region] = handler;
        }

        private void RemoveRegionVisual(DataExtractionRegionViewModel region)
        {
            if (_regionVisuals.TryGetValue(region, out var visual))
            {
                PART_Overlay.Children.Remove(visual.Root);
                _regionVisuals.Remove(region);
            }

            if (_regionHandlers.TryGetValue(region, out var handler))
            {
                region.PropertyChanged -= handler;
                _regionHandlers.Remove(region);
            }
        }

        private void UpdateAllRegionVisuals()
        {
            foreach (var region in _regionVisuals.Keys)
            {
                UpdateRegionVisual(region);
            }

            UpdateHighlightStates();
        }

        private void UpdateRegionVisual(DataExtractionRegionViewModel region)
        {
            if (!_regionVisuals.TryGetValue(region, out var visual))
                return;

            if (_pageWidth <= 0 || _pageHeight <= 0)
                return;

            if (region.PageNumber != PageNumber)
            {
                visual.Root.Visibility = System.Windows.Visibility.Hidden;
                return;
            }

            var left = region.X * _pageWidth;
            var top = region.Y * _pageHeight;
            var width = Math.Max(1d, region.Width * _pageWidth);
            var height = Math.Max(1d, region.Height * _pageHeight);

            System.Windows.Controls.Canvas.SetLeft(visual.Root, left);
            System.Windows.Controls.Canvas.SetTop(visual.Root, top);
            visual.Root.Width = width;
            visual.Root.Height = height;
            visual.Root.Visibility = System.Windows.Visibility.Visible;
            visual.UpdateHandles();
        }

        private void UpdateHighlightStates()
        {
            foreach (var pair in _regionVisuals)
            {
                pair.Value.SetSelected(ReferenceEquals(pair.Key, SelectedRegion));
            }
        }

        private void UpdateOverlaySize()
        {
            PART_Overlay.Width = _pageWidth;
            PART_Overlay.Height = _pageHeight;
        }

        private void SetSelectedRegion(DataExtractionRegionViewModel region)
        {
            SetCurrentValue(SelectedRegionProperty, region);
        }

        private void MoveRegion(DataExtractionRegionViewModel region, double deltaX, double deltaY)
        {
            if (_pageWidth <= 0 || _pageHeight <= 0)
                return;

            var update = new PdfRegionUpdate(region,
                                             region.PageNumber,
                                             region.X + (deltaX / _pageWidth),
                                             region.Y + (deltaY / _pageHeight),
                                             region.Width,
                                             region.Height);

            ExecuteUpdate(update);
        }

        private void ResizeRegion(DataExtractionRegionViewModel region, double deltaX, double deltaY, ResizeHandle handle)
        {
            if (_pageWidth <= 0 || _pageHeight <= 0)
                return;

            var left = region.X * _pageWidth;
            var top = region.Y * _pageHeight;
            var width = region.Width * _pageWidth;
            var height = region.Height * _pageHeight;

            switch (handle)
            {
                case ResizeHandle.TopLeft:
                    left += deltaX;
                    width -= deltaX;
                    top += deltaY;
                    height -= deltaY;
                    break;
                case ResizeHandle.TopRight:
                    width += deltaX;
                    top += deltaY;
                    height -= deltaY;
                    break;
                case ResizeHandle.BottomLeft:
                    left += deltaX;
                    width -= deltaX;
                    height += deltaY;
                    break;
                case ResizeHandle.BottomRight:
                    width += deltaX;
                    height += deltaY;
                    break;
            }

            width = Math.Max(4d, width);
            height = Math.Max(4d, height);

            var update = new PdfRegionUpdate(region,
                                             region.PageNumber,
                                             left / _pageWidth,
                                             top / _pageHeight,
                                             width / _pageWidth,
                                             height / _pageHeight);

            ExecuteUpdate(update);
        }

        private void ExecuteUpdate(PdfRegionUpdate update)
        {
            if (UpdateRegionCommand is ICommand command && command.CanExecute(update))
            {
                command.Execute(update);
            }
        }

        private void ExecuteCreate(PdfRegionDraft draft)
        {
            if (CreateRegionCommand is ICommand command && command.CanExecute(draft))
            {
                command.Execute(draft);
            }
        }

        private void OnOverlayMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!IsCreateRegionMode)
                return;

            if (_pageWidth <= 0 || _pageHeight <= 0)
                return;

            _isDrawing = true;
            _drawStart = e.GetPosition(PART_Overlay);
            _drawRectangle = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.DodgerBlue,
                StrokeThickness = 1,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 30, 144, 255))
            };

            System.Windows.Controls.Canvas.SetLeft(_drawRectangle, _drawStart.X);
            System.Windows.Controls.Canvas.SetTop(_drawRectangle, _drawStart.Y);
            PART_Overlay.Children.Add(_drawRectangle);
            PART_Overlay.CaptureMouse();
            e.Handled = true;
        }

        private void OnOverlayMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDrawing || _drawRectangle is null)
                return;

            var position = e.GetPosition(PART_Overlay);
            var left = Math.Min(position.X, _drawStart.X);
            var top = Math.Min(position.Y, _drawStart.Y);
            var width = Math.Abs(position.X - _drawStart.X);
            var height = Math.Abs(position.Y - _drawStart.Y);

            System.Windows.Controls.Canvas.SetLeft(_drawRectangle, left);
            System.Windows.Controls.Canvas.SetTop(_drawRectangle, top);
            _drawRectangle.Width = width;
            _drawRectangle.Height = height;
        }

        private void OnOverlayMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDrawing)
                return;

            _isDrawing = false;
            PART_Overlay.ReleaseMouseCapture();

            if (_drawRectangle is not null)
            {
                var width = _drawRectangle.Width;
                var height = _drawRectangle.Height;
                if (width > 6d && height > 6d && _pageWidth > 0 && _pageHeight > 0)
                {
                    var left = System.Windows.Controls.Canvas.GetLeft(_drawRectangle);
                    var top = System.Windows.Controls.Canvas.GetTop(_drawRectangle);
                    var draft = new PdfRegionDraft(
                        PageNumber,
                        left / _pageWidth,
                        top / _pageHeight,
                        width / _pageWidth,
                        height / _pageHeight);
                    ExecuteCreate(draft);
                }

                PART_Overlay.Children.Remove(_drawRectangle);
                _drawRectangle = null;
            }

            e.Handled = true;
        }

        private void UpdateOverlayCursor()
        {
            PART_Overlay.Cursor = IsCreateRegionMode
                ? System.Windows.Input.Cursors.Cross
                : System.Windows.Input.Cursors.Arrow;
        }

        private void DisposeDocument()
        {
            if (_document is not null)
            {
                _document.Dispose();
                _document = null;
            }
        }

        private enum ResizeHandle
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private sealed class RegionVisual
        {
            private readonly PdfDocumentView _owner;
            private readonly DataExtractionRegionViewModel _region;
            private readonly System.Windows.Controls.Border _outline;
            private readonly System.Windows.Controls.Primitives.Thumb _moveThumb;
            private readonly System.Windows.Controls.Primitives.Thumb _topLeft;
            private readonly System.Windows.Controls.Primitives.Thumb _topRight;
            private readonly System.Windows.Controls.Primitives.Thumb _bottomLeft;
            private readonly System.Windows.Controls.Primitives.Thumb _bottomRight;

            public RegionVisual(PdfDocumentView owner, DataExtractionRegionViewModel region)
            {
                _owner = owner;
                _region = region;

                Root = new System.Windows.Controls.Grid
                {
                    Background = System.Windows.Media.Brushes.Transparent
                };

            _outline = new System.Windows.Controls.Border
            {
                BorderBrush = System.Windows.Media.Brushes.DodgerBlue,
                BorderThickness = new System.Windows.Thickness(1),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 30, 144, 255))
            };
            _outline.MouseLeftButtonDown += (_, args) =>
            {
                _owner.SetSelectedRegion(_region);
                args.Handled = true;
            };
            Root.Children.Add(_outline);

                _moveThumb = CreateMoveThumb();
                _moveThumb.DragDelta += OnMoveDrag;
                _moveThumb.DragStarted += OnDragStarted;
                Root.Children.Add(_moveThumb);

                _topLeft = CreateHandleThumb(System.Windows.Input.Cursors.SizeNWSE);
                _topLeft.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                _topLeft.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                _topLeft.Margin = new System.Windows.Thickness(-6, -6, 0, 0);
                _topLeft.DragDelta += (_, args) => _owner.ResizeRegion(_region, args.HorizontalChange, args.VerticalChange, ResizeHandle.TopLeft);
                _topLeft.DragStarted += OnDragStarted;
                Root.Children.Add(_topLeft);

                _topRight = CreateHandleThumb(System.Windows.Input.Cursors.SizeNESW);
                _topRight.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                _topRight.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                _topRight.Margin = new System.Windows.Thickness(0, -6, -6, 0);
                _topRight.DragDelta += (_, args) => _owner.ResizeRegion(_region, args.HorizontalChange, args.VerticalChange, ResizeHandle.TopRight);
                _topRight.DragStarted += OnDragStarted;
                Root.Children.Add(_topRight);

                _bottomLeft = CreateHandleThumb(System.Windows.Input.Cursors.SizeNESW);
                _bottomLeft.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                _bottomLeft.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                _bottomLeft.Margin = new System.Windows.Thickness(-6, 0, 0, -6);
                _bottomLeft.DragDelta += (_, args) => _owner.ResizeRegion(_region, args.HorizontalChange, args.VerticalChange, ResizeHandle.BottomLeft);
                _bottomLeft.DragStarted += OnDragStarted;
                Root.Children.Add(_bottomLeft);

                _bottomRight = CreateHandleThumb(System.Windows.Input.Cursors.SizeNWSE);
                _bottomRight.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                _bottomRight.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                _bottomRight.Margin = new System.Windows.Thickness(0, 0, -6, -6);
                _bottomRight.DragDelta += (_, args) => _owner.ResizeRegion(_region, args.HorizontalChange, args.VerticalChange, ResizeHandle.BottomRight);
                _bottomRight.DragStarted += OnDragStarted;
                Root.Children.Add(_bottomRight);
            }

            public System.Windows.Controls.Grid Root { get; }

            public void UpdateHandles()
            {
                // alignment handled via margins; nothing dynamic required currently.
            }

            public void SetSelected(bool isSelected)
            {
                if (isSelected)
                {
                    _outline.BorderBrush = System.Windows.Media.Brushes.OrangeRed;
                    _outline.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 140, 0));
                }
                else
                {
                    _outline.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
                    _outline.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 30, 144, 255));
                }
            }

            private void OnMoveDrag(object? sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
            {
                _owner.MoveRegion(_region, e.HorizontalChange, e.VerticalChange);
            }

            private void OnDragStarted(object? sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            {
                _owner.SetSelectedRegion(_region);
            }

            private static System.Windows.Controls.Primitives.Thumb CreateMoveThumb()
            {
                var thumb = new System.Windows.Controls.Primitives.Thumb
                {
                    Background = System.Windows.Media.Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.SizeAll
                };

                return thumb;
            }

            private static System.Windows.Controls.Primitives.Thumb CreateHandleThumb(System.Windows.Input.Cursor cursor)
            {
                var thumb = new System.Windows.Controls.Primitives.Thumb
                {
                    Width = 12,
                    Height = 12,
                    Cursor = cursor
                };

                var template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb));
                var rectangleFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Shapes.Rectangle));
                rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.FillProperty, System.Windows.Media.Brushes.White);
                rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.StrokeProperty, System.Windows.Media.Brushes.DodgerBlue);
                rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.StrokeThicknessProperty, 1d);
                template.VisualTree = rectangleFactory;
                thumb.Template = template;

                return thumb;
            }
        }
    }
}
