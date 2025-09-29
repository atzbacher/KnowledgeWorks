using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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

        public Task<string?> LoadPdfAsync()
        {
            return Task.FromResult(DocumentSource?.AbsoluteUri);
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
        }

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
