using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Pdf;
using Microsoft.Web.WebView2.Core;

namespace LM.App.Wpf.Views
{
    public partial class PdfViewer : System.Windows.Controls.UserControl
    {
        private static readonly string ViewerRelativePath = Path.Combine("wwwroot", "pdfjs", "web", "viewer.html");

        private PdfViewerViewModel? _viewModel;
        private System.Uri? _pendingDocumentSource;
        private bool _isBridgeInitialized;
        private PdfWebViewBridge? _bridge;

        public PdfViewer()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.WebViewBridge = null;
            }

            _viewModel = e.NewValue as PdfViewerViewModel;

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _pendingDocumentSource = _viewModel.DocumentSource;
                AttachBridge();
            }

            if (IsLoaded)
            {
                _ = PdfWebView.Dispatcher.InvokeAsync(async () =>
                {
                    await UpdateViewerAsync(_viewModel?.DocumentSource).ConfigureAwait(true);
                });
            }
        }

        private void OnUnloaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.WebViewBridge = null;
            }

            if (PdfWebView.CoreWebView2 is not null && _isBridgeInitialized)
            {
                PdfWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _isBridgeInitialized = false;
            }
        }

        private void OnLoaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            var documentSource = _viewModel?.DocumentSource ?? _pendingDocumentSource;
            _pendingDocumentSource = null;

            AttachBridge();

            _ = PdfWebView.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateViewerAsync(documentSource).ConfigureAwait(true);
            });
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PdfViewerViewModel.DocumentSource))
            {
                _pendingDocumentSource = _viewModel?.DocumentSource;

                if (!IsLoaded)
                {
                    return;
                }

                _ = PdfWebView.Dispatcher.InvokeAsync(async () =>
                {
                    await UpdateViewerAsync(_viewModel?.DocumentSource).ConfigureAwait(true);
                });
            }
        }

        private async Task UpdateViewerAsync(System.Uri? documentSource)
        {
            if (!IsLoaded)
            {
                return;
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var viewerPath = Path.Combine(baseDirectory, ViewerRelativePath);

            if (!File.Exists(viewerPath))
            {
                Trace.TraceWarning("Pdf.js viewer asset was not found at '{0}'.", viewerPath);
                return;
            }

            try
            {
                await PdfWebView.EnsureCoreWebView2Async().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to initialize WebView2: {0}", ex);
                return;
            }

            if (PdfWebView.CoreWebView2 is null)
            {
                return;
            }

            InitializeBridge(PdfWebView.CoreWebView2);

            var viewerUri = new Uri(viewerPath, UriKind.Absolute);
            var target = viewerUri.AbsoluteUri;

            if (documentSource is not null)
            {
                var encodedPdf = Uri.EscapeDataString(documentSource.AbsoluteUri);
                target = string.Concat(viewerUri.AbsoluteUri, "?file=", encodedPdf);
            }

            PdfWebView.CoreWebView2.Navigate(target);
        }

        private void InitializeBridge(CoreWebView2 coreWebView)
        {
            if (_isBridgeInitialized)
            {
                return;
            }

            coreWebView.WebMessageReceived += OnWebMessageReceived;
            _isBridgeInitialized = true;
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_viewModel is null)
            {
                return;
            }

            try
            {
                var json = e.WebMessageAsJson;
                if (string.IsNullOrWhiteSpace(json))
                {
                    json = e.TryGetWebMessageAsString();
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("type", out var typeElement) || typeElement.GetString() != "highlight-created")
                {
                    return;
                }

                if (!root.TryGetProperty("annotationId", out var annotationElement))
                {
                    return;
                }

                var annotationId = annotationElement.GetString();
                if (string.IsNullOrWhiteSpace(annotationId))
                {
                    return;
                }

                if (!root.TryGetProperty("preview", out var previewElement))
                {
                    return;
                }

                if (!previewElement.TryGetProperty("base64", out var dataElement))
                {
                    return;
                }

                var base64 = dataElement.GetString();
                if (string.IsNullOrWhiteSpace(base64))
                {
                    return;
                }

                var width = previewElement.TryGetProperty("width", out var widthElement) ? widthElement.GetInt32() : 0;
                var height = previewElement.TryGetProperty("height", out var heightElement) ? heightElement.GetInt32() : 0;

                var pngBytes = Convert.FromBase64String(base64);

                await _viewModel.HandleHighlightPreviewAsync(annotationId, pngBytes, width, height).ConfigureAwait(true);
            }
            catch (FormatException ex)
            {
                Trace.TraceError("Failed to decode highlight preview payload: {0}", ex);
            }
            catch (JsonException ex)
            {
                Trace.TraceError("Failed to parse WebView message: {0}", ex);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unhandled error while processing WebView message: {0}", ex);
            }
        }

        private void AttachBridge()
        {
            if (_viewModel is null)
            {
                return;
            }

            _bridge ??= new PdfWebViewBridge(PdfWebView);
            _viewModel.WebViewBridge = _bridge;
        }

        private sealed class PdfWebViewBridge : IPdfWebViewBridge
        {
            private readonly Microsoft.Web.WebView2.Wpf.WebView2 _webView;

            public PdfWebViewBridge(Microsoft.Web.WebView2.Wpf.WebView2 webView)
            {
                _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            }

            public async Task ScrollToAnnotationAsync(string annotationId, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(annotationId))
                {
                    return;
                }

                await EnsureCoreAsync().ConfigureAwait(false);

                await _webView.Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_webView.CoreWebView2 is null)
                    {
                        return;
                    }

                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "scroll-to-annotation",
                        annotationId
                    });

                    _webView.CoreWebView2.PostWebMessageAsJson(payload);
                }).Task.ConfigureAwait(false);
            }

            private async Task EnsureCoreAsync()
            {
                if (_webView.CoreWebView2 is not null)
                {
                    return;
                }

                if (!_webView.Dispatcher.CheckAccess())
                {
                    await _webView.Dispatcher.InvokeAsync(async () =>
                    {
                        await EnsureCoreAsync().ConfigureAwait(true);
                    }).Task.ConfigureAwait(false);
                    return;
                }

                await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);
            }
        }
    }
}
