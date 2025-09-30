using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels.Pdf
{
    internal sealed partial class PdfViewerViewModel
    {
        private bool _isViewerReady;
        private string? _currentSelectionText;
        private int? _selectionPageNumber;
        private int _currentPageNumber;
        private string? _overlayJson;
        private string? _overlaySidecarPath;
        private System.Uri? _virtualDocumentSource;
        private TaskCompletionSource<System.Uri?>? _virtualDocumentSourceReady;
        private bool _pendingDocumentLoadRequest;

        public bool IsViewerReady
        {
            get => _isViewerReady;
            private set => SetProperty(ref _isViewerReady, value);
        }

        public string? CurrentSelectionText
        {
            get => _currentSelectionText;
            private set => SetProperty(ref _currentSelectionText, string.IsNullOrWhiteSpace(value) ? null : value);
        }

        public int? SelectionPageNumber
        {
            get => _selectionPageNumber;
            private set => SetProperty(ref _selectionPageNumber, value);
        }

        public int CurrentPageNumber
        {
            get => _currentPageNumber;
            private set => SetProperty(ref _currentPageNumber, value);
        }

        public string? OverlayJson
        {
            get => _overlayJson;
            private set => SetProperty(ref _overlayJson, string.IsNullOrWhiteSpace(value) ? null : value);
        }

        public string? OverlaySidecarPath
        {
            get => _overlaySidecarPath;
            private set => SetProperty(ref _overlaySidecarPath, string.IsNullOrWhiteSpace(value) ? null : value);
        }

        public async Task<string?> LoadPdfAsync()
        {
            var completion = _virtualDocumentSourceReady;

            if (completion is not null)
            {
                var readyTask = completion.Task;
                var completedTask = await Task.WhenAny(readyTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(true);

                if (completedTask == readyTask)
                {
                    var virtualSource = await readyTask.ConfigureAwait(true);
                    if (virtualSource is not null)
                    {
                        return virtualSource.AbsoluteUri;
                    }
                }
            }

            var fallback = _virtualDocumentSource ?? DocumentSource;

            if (fallback is not null && string.Equals(fallback.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fallback?.AbsoluteUri;
        }

        public Task<string?> CreateHighlightAsync(string payloadJson)
        {
            var annotation = EnsureAnnotationFromPayload(payloadJson);
            return Task.FromResult(annotation?.Id);
        }

        public Task<string?> GetCurrentSelectionAsync()
        {
            if (CurrentSelectionText is null && SelectionPageNumber is null)
            {
                return Task.FromResult<string?>(null);
            }

            var snapshot = new
            {
                text = CurrentSelectionText,
                pageNumber = SelectionPageNumber,
            };

            var json = JsonSerializer.Serialize(snapshot);
            return Task.FromResult<string?>(json);
        }

        public Task SetOverlayAsync(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                UpdateOverlaySnapshot(null, null);
                return Task.CompletedTask;
            }

            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;

                var overlayJson = root.TryGetProperty("overlay", out var overlayElement)
                    ? overlayElement.GetRawText()
                    : root.GetRawText();

                var sidecarPath = root.TryGetProperty("sidecarPath", out var sidecarElement)
                    ? sidecarElement.GetString()
                    : null;

                UpdateOverlaySnapshot(overlayJson, sidecarPath);
            }
            catch (JsonException ex)
            {
                Trace.TraceError("Failed to parse overlay payload: {0}", ex);
            }

            return Task.CompletedTask;
        }

        internal void HandleViewerReady()
        {
            IsViewerReady = true;
            TryRequestDocumentLoad();
        }

        internal void UpdateVirtualDocumentSource(System.Uri? virtualSource)
        {
            _virtualDocumentSource = virtualSource;
            _virtualDocumentSourceReady?.TrySetResult(virtualSource);
            TryRequestDocumentLoad();
        }

        partial void OnDocumentSourceChanged(System.Uri? value)
        {
            if (value is null)
            {
                _virtualDocumentSource = null;
                var completion = CreateVirtualDocumentCompletion();
                completion.TrySetResult(null);
                _virtualDocumentSourceReady = completion;
                _pendingDocumentLoadRequest = false;
                return;
            }

            _pendingDocumentLoadRequest = true;

            if (!value.IsAbsoluteUri || !string.Equals(value.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                _virtualDocumentSource = value;
                var completion = CreateVirtualDocumentCompletion();
                completion.TrySetResult(value);
                _virtualDocumentSourceReady = completion;
                TryRequestDocumentLoad();
                return;
            }

            _virtualDocumentSource = null;
            _virtualDocumentSourceReady = CreateVirtualDocumentCompletion();
            TryRequestDocumentLoad();
        }

        private static TaskCompletionSource<System.Uri?> CreateVirtualDocumentCompletion()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void UpdateSelection(string? text, int? pageNumber)
        {
            CurrentSelectionText = text;
            SelectionPageNumber = pageNumber;
        }

        internal void HandleNavigationChanged(int pageNumber)
        {
            if (pageNumber < 1)
            {
                return;
            }

            CurrentPageNumber = pageNumber;
        }

        private void TryRequestDocumentLoad()
        {
            if (!_pendingDocumentLoadRequest)
            {
                return;
            }

            if (!IsViewerReady)
            {
                return;
            }

            var bridge = _webViewBridge;
            if (bridge is null)
            {
                return;
            }

            _pendingDocumentLoadRequest = false;
            _ = bridge.RequestDocumentLoadAsync(CancellationToken.None);
        }

        internal void HandleHighlightCreated(string annotationId, int? pageNumber)
        {
            if (string.IsNullOrWhiteSpace(annotationId))
            {
                return;
            }

            var annotation = Annotations.FirstOrDefault(a => string.Equals(a.Id, annotationId, StringComparison.OrdinalIgnoreCase));
            if (annotation is not null && pageNumber.HasValue && pageNumber.Value > 0)
            {
                annotation.PageNumber = pageNumber.Value;
            }
        }

        private void UpdateOverlaySnapshot(string? overlayJson, string? sidecarPath)
        {
            OverlayJson = overlayJson;
            OverlaySidecarPath = sidecarPath;
        }

        private PdfAnnotation? EnsureAnnotationFromPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;

                var annotationId = root.TryGetProperty("annotationId", out var idElement)
                    ? idElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(annotationId))
                {
                    annotationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                }

                annotationId = annotationId!.Trim();

                var title = root.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(title))
                {
                    var pageNumberForTitle = root.TryGetProperty("pageNumber", out var pageElement) && pageElement.TryGetInt32(out var parsedPage)
                        ? parsedPage
                        : 0;

                    title = pageNumberForTitle > 0
                        ? string.Format(CultureInfo.InvariantCulture, "Highlight on page {0}", pageNumberForTitle)
                        : "Highlight";
                }

                var annotation = Annotations.FirstOrDefault(a => string.Equals(a.Id, annotationId, StringComparison.OrdinalIgnoreCase));
                if (annotation is null)
                {
                    annotation = new PdfAnnotation(annotationId, title!);
                    Annotations.Add(annotation);
                }
                else if (!string.IsNullOrWhiteSpace(title))
                {
                    annotation.Title = title!;
                }

                if (root.TryGetProperty("pageNumber", out var pageNumberElement) && pageNumberElement.TryGetInt32(out var pageNumber) && pageNumber > 0)
                {
                    annotation.PageNumber = pageNumber;
                }

                if (root.TryGetProperty("textSnippet", out var textElement))
                {
                    annotation.TextSnippet = textElement.GetString();
                }

                if (root.TryGetProperty("note", out var noteElement))
                {
                    annotation.Note = noteElement.GetString();
                }

                if (root.TryGetProperty("color", out var colorElement))
                {
                    annotation.ColorName = colorElement.GetString();
                }

                return annotation;
            }
            catch (JsonException ex)
            {
                Trace.TraceError("Failed to deserialize highlight payload: {0}", ex);
                return null;
            }
        }
    }
}
