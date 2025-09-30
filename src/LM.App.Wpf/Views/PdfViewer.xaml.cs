using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Pdf;
using Microsoft.Web.WebView2.Core;

namespace LM.App.Wpf.Views
{
    public partial class PdfViewer : System.Windows.Controls.UserControl
    {
        private const string ViewerVirtualHostName = "viewer-appassets.knowledgeworks";
        private const string DocumentVirtualHostName = "viewer-documents.knowledgeworks";
        private static readonly string ViewerRelativePath = Path.Combine("wwwroot", "pdfjs", "web", "viewer.html");

        private PdfViewerViewModel? _viewModel;
        private System.Uri? _pendingDocumentSource;
        private bool _isBridgeInitialized;
        private PdfWebViewBridge? _bridge;
        private PdfViewerHostObject? _hostObject;
        private string? _mappedWebRootPath;
        private string? _currentDocumentFilePath;
        private string? _currentDocumentToken;
        private bool _isDocumentRequestHandlerAttached;

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

            if (PdfWebView.CoreWebView2 is not null && _isDocumentRequestHandlerAttached)
            {
                try
                {
                    PdfWebView.CoreWebView2.RemoveWebResourceRequestedFilter(
                        string.Concat("https://", DocumentVirtualHostName, "/*"),
                        CoreWebView2WebResourceContext.All);
                }
                catch (NotImplementedException)
                {
                }

                PdfWebView.CoreWebView2.WebResourceRequested -= OnDocumentWebResourceRequested;
                _isDocumentRequestHandlerAttached = false;
            }

            ClearDocumentRequestMapping();
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
            if (!string.Equals(e.PropertyName, nameof(PdfViewerViewModel.DocumentSource), StringComparison.Ordinal))
            {
                return;
            }

            _pendingDocumentSource = _viewModel?.DocumentSource;

            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(HandleDocumentSourceChanged);
                return;
            }

            HandleDocumentSourceChanged();
        }

        private void HandleDocumentSourceChanged()
        {
            if (!IsLoaded)
            {
                return;
            }

            var documentSource = _viewModel?.DocumentSource ?? _pendingDocumentSource;

            _ = PdfWebView.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateViewerAsync(documentSource).ConfigureAwait(true);
            });
        }

        private async Task UpdateViewerAsync(System.Uri? documentSource)
        {
            if (!IsLoaded)
            {
                return;
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var webRootPath = Path.Combine(baseDirectory, "wwwroot");
            var viewerPath = Path.Combine(baseDirectory, ViewerRelativePath);

            if (!File.Exists(viewerPath))
            {
                Trace.TraceWarning("Pdf.js viewer asset was not found at '{0}'.", viewerPath);
                return;
            }

            if (!Directory.Exists(webRootPath))
            {
                Trace.TraceWarning("Pdf.js web root directory was not found at '{0}'.", webRootPath);
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

            var coreWebView = PdfWebView.CoreWebView2;

            EnsureVirtualHostMapping(coreWebView, webRootPath);

            var viewerUri = new Uri(string.Concat("https://", ViewerVirtualHostName, "/pdfjs/web/viewer.html"), UriKind.Absolute);
            var target = viewerUri.AbsoluteUri;

            if (documentSource is null)
            {
                ClearDocumentRequestMapping();
            }

            var virtualDocumentSource = documentSource is null
                ? null
                : TryCreateVirtualDocumentUri(coreWebView, documentSource);

            _viewModel?.UpdateVirtualDocumentSource(virtualDocumentSource);

            if (virtualDocumentSource is not null)
            {
                var encodedPdf = Uri.EscapeDataString(virtualDocumentSource.AbsoluteUri);
                target = string.Concat(target, "?file=", encodedPdf);
            }

            InitializeBridge(coreWebView);

            coreWebView.Navigate(target);
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
            catch (COMException ex)
            {
                Trace.TraceWarning(
                    "WebView2 failed to remove existing host object (HRESULT: 0x{0:X8}). {1}",
                    ex.HResult,
                    ex);

            }

            _hostObject ??= new PdfViewerHostObject(this);

            var areHostObjectsAllowed = coreWebView.Settings.AreHostObjectsAllowed;
            Trace.TraceInformation("AreHostObjectsAllowed = {0}", areHostObjectsAllowed);
            Trace.TraceInformation(
                "PdfViewerHostObject COM visible: {0}",
                Marshal.IsTypeVisibleFromCom(typeof(PdfViewerHostObject)));

            try
            {

                Trace.TraceInformation(
    "PdfViewerHostObject visible to COM? {0}",
    Marshal.IsTypeVisibleFromCom(typeof(PdfViewer.PdfViewerHostObject)));

                try
                {
                    var typeInfo = Marshal.GetIUnknownForObject(typeof(PdfViewer.PdfViewerHostObject));
                    Trace.TraceInformation("GetITypeInfoForType succeeded (ptr = {0}).", typeInfo);
                    Marshal.Release(typeInfo);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("GetITypeInfoForType failed: {0}", ex);
                }

                try
                {
                    _ = Marshal.GetIDispatchForObject(_hostObject!);
                    Trace.TraceInformation("GetIDispatchForObject succeeded.");
                }
                catch (Exception ex)
                {
                    Trace.TraceError("GetIDispatchForObject failed: {0}", ex);
                }

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

        private void EnsureVirtualHostMapping(CoreWebView2 coreWebView, string webRootPath)
        {
            if (coreWebView is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(webRootPath) || !Directory.Exists(webRootPath))
            {
                return;
            }

            var fullPath = Path.GetFullPath(webRootPath);

            if (string.Equals(_mappedWebRootPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                coreWebView.SetVirtualHostNameToFolderMapping(
                    ViewerVirtualHostName,
                    fullPath,
                    CoreWebView2HostResourceAccessKind.Allow);
                _mappedWebRootPath = fullPath;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to map WebView virtual host '{0}' to '{1}': {2}", ViewerVirtualHostName, fullPath, ex);
            }
        }

        private System.Uri? TryCreateVirtualDocumentUri(CoreWebView2 coreWebView, System.Uri documentSource)
        {
            if (coreWebView is null)
            {
                return null;
            }

            if (!documentSource.IsAbsoluteUri)
            {
                ClearDocumentRequestMapping();
                return documentSource;
            }

            if (!string.Equals(documentSource.Scheme, System.Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                ClearDocumentRequestMapping();
                return documentSource;
            }

            var pdfPath = documentSource.LocalPath;
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                ClearDocumentRequestMapping();
                return null;
            }

            var fileName = Path.GetFileName(pdfPath);
            if (string.IsNullOrEmpty(fileName))
            {
                ClearDocumentRequestMapping();
                return null;
            }

            EnsureDocumentRequestHandler(coreWebView);

            var token = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            _currentDocumentFilePath = pdfPath;
            _currentDocumentToken = token;

            var targetPath = string.Concat(token, "/", Uri.EscapeDataString(fileName));
            var target = string.Concat("https://", DocumentVirtualHostName, "/", targetPath);
            return new System.Uri(target, UriKind.Absolute);
        }

        private void EnsureDocumentRequestHandler(CoreWebView2 coreWebView)
        {
            if (_isDocumentRequestHandlerAttached)
            {
                return;
            }

            var documentHost = string.Concat("https://", DocumentVirtualHostName, "/*");

            try
            {
                coreWebView.AddWebResourceRequestedFilter(documentHost, CoreWebView2WebResourceContext.All);
            }
            catch (NotImplementedException)
            {
            }

            coreWebView.WebResourceRequested += OnDocumentWebResourceRequested;
            _isDocumentRequestHandlerAttached = true;
        }

        private void OnDocumentWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var environment = PdfWebView.CoreWebView2?.Environment;
            if (environment is null)
            {
                return;
            }

            if (!TryResolveDocumentRequest(e.Request.Uri, out var resolvedPath))
            {
                return;
            }

            CoreWebView2Deferral? deferral = null;

            try
            {
                deferral = e.GetDeferral();

                if (!File.Exists(resolvedPath))
                {
                    var notFoundPayload = Encoding.UTF8.GetBytes("File not found.");
                    var notFoundHeaders = BuildResponseHeaders("text/plain; charset=utf-8", notFoundPayload.Length, allowRange: false);
                    var payloadStream = new MemoryStream(notFoundPayload, writable: false);
                    e.Response = environment.CreateWebResourceResponse(payloadStream, 404, "Not Found", notFoundHeaders);

                    return;
                }

                var fileInfo = new FileInfo(resolvedPath);
                var rangeHeader = e.Request.Headers.GetHeader("Range");

                if (TryCreateRangeResponse(environment, resolvedPath, fileInfo.Length, rangeHeader, out var rangeResponse))
                {
                    e.Response = rangeResponse;
                    return;
                }

                var fileStreamOptions = new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    BufferSize = 81920,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                };

                var fileStream = new FileStream(resolvedPath, fileStreamOptions);
                var headers = BuildResponseHeaders("application/pdf", fileInfo.Length, allowRange: true);
                e.Response = environment.CreateWebResourceResponse(fileStream, 200, "OK", headers);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to serve PDF '{0}': {1}", resolvedPath ?? string.Empty, ex);
            }
            finally
            {
                deferral?.Complete();
            }
        }

        private static string BuildResponseHeaders(string contentType, long contentLength, bool allowRange)
        {
            var builder = new StringBuilder();
            builder.Append("Content-Type: ");
            builder.Append(contentType);
            builder.Append('\r');
            builder.Append('\n');

            builder.Append("Content-Length: ");
            builder.Append(contentLength.ToString(CultureInfo.InvariantCulture));
            builder.Append('\r');
            builder.Append('\n');

            if (allowRange)
            {
                builder.Append("Accept-Ranges: bytes\r\n");
            }

            builder.Append("Access-Control-Allow-Origin: *\r\n");
            builder.Append("Access-Control-Allow-Credentials: false\r\n");

            return builder.ToString();
        }

        private static bool TryCreateRangeResponse(CoreWebView2Environment environment, string path, long fileLength, string? rangeHeader, out CoreWebView2WebResourceResponse? response)
        {
            response = null;

            if (string.IsNullOrWhiteSpace(rangeHeader))
            {
                return false;
            }

            const string prefix = "bytes=";
            if (!rangeHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rangeExpression = rangeHeader.Substring(prefix.Length);
            var tokens = rangeExpression.Split('-', 2, StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            long start;
            long end = fileLength - 1;

            if (!long.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
            {
                if (tokens.Length <= 1 || string.IsNullOrWhiteSpace(tokens[1]) || !long.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffixLength))
                {
                    return false;
                }

                if (suffixLength <= 0)
                {
                    return false;
                }

                var lengthFromEnd = Math.Min(suffixLength, fileLength);
                start = fileLength - lengthFromEnd;
            }

            if (start < 0 || start >= fileLength)
            {
                return false;
            }

            if (tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1]) && long.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnd))
            {
                if (parsedEnd < start)
                {
                    return false;
                }

                end = Math.Min(parsedEnd, fileLength - 1);
            }

            var length = (end - start) + 1;

            var streamOptions = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = 81920,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };

            var stream = new FileStream(path, streamOptions);
            stream.Seek(start, SeekOrigin.Begin);

            var builder = new StringBuilder();
            builder.Append("Content-Type: application/pdf\r\n");
            builder.Append("Accept-Ranges: bytes\r\n");
            builder.Append("Access-Control-Allow-Origin: *\r\n");
            builder.Append("Access-Control-Allow-Credentials: false\r\n");
            builder.Append("Content-Length: ");
            builder.Append(length.ToString(CultureInfo.InvariantCulture));
            builder.Append('\r');
            builder.Append('\n');
            builder.Append("Content-Range: bytes ");
            builder.Append(start.ToString(CultureInfo.InvariantCulture));
            builder.Append('-');
            builder.Append(end.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(fileLength.ToString(CultureInfo.InvariantCulture));
            builder.Append('\r');
            builder.Append('\n');

            response = environment.CreateWebResourceResponse(stream, 206, "Partial Content", builder.ToString());
            return true;
        }

        private bool TryResolveDocumentRequest(string? requestUri, out string? path)
        {
            path = null;

            if (string.IsNullOrWhiteSpace(requestUri))
            {
                return false;
            }

            if (_currentDocumentToken is null || _currentDocumentFilePath is null)
            {
                return false;
            }

            if (!System.Uri.TryCreate(requestUri, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Host, DocumentVirtualHostName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            if (!string.Equals(segments[0], _currentDocumentToken, StringComparison.Ordinal))
            {
                return false;
            }

            var requestedFileName = segments.Length > 1
                ? Uri.UnescapeDataString(segments[^1])
                : string.Empty;

            var currentFileName = Path.GetFileName(_currentDocumentFilePath);

            if (!string.Equals(requestedFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = _currentDocumentFilePath;
            return true;
        }

        private void ClearDocumentRequestMapping()
        {
            _currentDocumentFilePath = null;
            _currentDocumentToken = null;
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

            public async Task RequestDocumentLoadAsync(CancellationToken cancellationToken)
            {
                await EnsureCoreAsync().ConfigureAwait(false);

                await _webView.Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_webView.CoreWebView2 is null)
                    {
                        return;
                    }

                    _ = _webView.CoreWebView2.ExecuteScriptAsync("window?.PdfBridge?.loadPdf?.()");
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
