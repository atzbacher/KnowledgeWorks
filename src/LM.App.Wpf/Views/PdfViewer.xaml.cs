using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private PdfViewerHostObject? _hostObject;

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

            try
            {
                coreWebView.RemoveHostObjectFromScript("knowledgeworksBridge");
            }
            catch (ArgumentException)
            {
                // Host object was not previously registered; ignore.
            }
            catch (NotImplementedException)
            {
                // Older runtimes may not support removal; ignore.
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0x80070049)
            {
                // WebView2 returns ERROR_BAD_NET_NAME (0x80070049) when the
                // script object is no longer available. This happens if the
                // viewer closes before the host object is disposed. Ignore it
                // and continue registering a new bridge instance.
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0x80070490)
            {
                // WebView2 returns ERROR_NOT_FOUND (0x80070490) if the
                // host object has already been removed. Treat it the same
                // as a missing registration and continue.
            }

            _hostObject ??= new PdfViewerHostObject(this);

            try
            {
                coreWebView.AddHostObjectToScript("knowledgeworksBridge", _hostObject);
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError("Failed to register WebView host object: {0}", ex);
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

                if (!root.TryGetProperty("type", out var typeElement))
                {
                    return;
                }

                var messageType = typeElement.GetString();
                switch (messageType)
                {
                    case "ready":
                        _viewModel.HandleViewerReady();
                        break;
                    case "selection-changed":
                        HandleSelectionChanged(root);
                        break;
                    case "highlight-created":
                        await HandleHighlightCreatedAsync(root).ConfigureAwait(true);
                        break;
                    case "nav-changed":
                        HandleNavigationChanged(root);
                        break;
                    default:
                        break;
                }
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

        private void HandleSelectionChanged(JsonElement root)
        {
            if (_viewModel is null)
            {
                return;
            }

            if (!root.TryGetProperty("selection", out var selectionElement) || selectionElement.ValueKind == JsonValueKind.Null)
            {
                _viewModel.UpdateSelection(null, null);
                return;
            }

            var text = selectionElement.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;

            int? pageNumber = null;
            if (selectionElement.TryGetProperty("pageNumber", out var pageElement) && pageElement.ValueKind == JsonValueKind.Number)
            {
                if (pageElement.TryGetInt32(out var parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                }
            }

            _viewModel.UpdateSelection(text, pageNumber);
        }

        private async Task HandleHighlightCreatedAsync(JsonElement root)
        {
            if (_viewModel is null)
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

            int? pageNumber = null;
            if (root.TryGetProperty("pageIndex", out var pageIndexElement) && pageIndexElement.ValueKind == JsonValueKind.Number)
            {
                if (pageIndexElement.TryGetInt32(out var pageIndex) && pageIndex >= 0)
                {
                    pageNumber = pageIndex + 1;
                }
            }

            _viewModel.HandleHighlightCreated(annotationId, pageNumber);

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

            var width = previewElement.TryGetProperty("width", out var widthElement) && widthElement.ValueKind == JsonValueKind.Number
                ? widthElement.GetInt32()
                : 0;
            var height = previewElement.TryGetProperty("height", out var heightElement) && heightElement.ValueKind == JsonValueKind.Number
                ? heightElement.GetInt32()
                : 0;

            var pngBytes = Convert.FromBase64String(base64);

            await _viewModel.HandleHighlightPreviewAsync(annotationId, pngBytes, width, height).ConfigureAwait(true);
        }

        private void HandleNavigationChanged(JsonElement root)
        {
            if (_viewModel is null)
            {
                return;
            }

            if (!root.TryGetProperty("pageNumber", out var pageElement) || pageElement.ValueKind != JsonValueKind.Number)
            {
                return;
            }

            if (pageElement.TryGetInt32(out var pageNumber) && pageNumber > 0)
            {
                _viewModel.HandleNavigationChanged(pageNumber);
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

        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.None)]
        private sealed class PdfViewerHostObject
        {
            private readonly PdfViewer _owner;

            public PdfViewerHostObject(PdfViewer owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public Task<string?> LoadPdfAsync()
            {
                return InvokeAsync(viewModel => viewModel.LoadPdfAsync());
            }

            public Task<string?> CreateHighlightAsync(string payloadJson)
            {
                return InvokeAsync(viewModel => viewModel.CreateHighlightAsync(payloadJson ?? string.Empty));
            }

            public Task<string?> GetCurrentSelectionAsync()
            {
                return InvokeAsync(viewModel => viewModel.GetCurrentSelectionAsync());
            }

            public Task SetOverlayAsync(string payloadJson)
            {
                return InvokeAsync(async viewModel =>
                {
                    await viewModel.SetOverlayAsync(payloadJson ?? string.Empty).ConfigureAwait(true);
                    return (string?)null;
                });
            }

            private Task<TResult?> InvokeAsync<TResult>(Func<PdfViewerViewModel, Task<TResult?>> callback)
            {
                var dispatcher = _owner.PdfWebView.Dispatcher;
                if (dispatcher.CheckAccess())
                {
                    return ExecuteAsync(callback);
                }

                return dispatcher.InvokeAsync(() => ExecuteAsync(callback)).Task.Unwrap();
            }

            private async Task<TResult?> ExecuteAsync<TResult>(Func<PdfViewerViewModel, Task<TResult?>> callback)
            {
                var viewModel = _owner._viewModel;
                if (viewModel is null)
                {
                    return default;
                }

                try
                {
                    return await callback(viewModel).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Host bridge invocation failed: {0}", ex);
                    return default;
                }
            }
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
