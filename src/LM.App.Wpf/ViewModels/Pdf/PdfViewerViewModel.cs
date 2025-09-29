using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.App.Wpf.ViewModels.Review;
using LM.Infrastructure.Hooks;
using LM.Core.Abstractions;

namespace LM.App.Wpf.ViewModels.Pdf
{
    /// <summary>
    /// Coordinates PDF rendering and annotation metadata for the viewer surface.
    /// </summary>
    internal sealed class PdfViewerViewModel : ViewModelBase
    {
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private readonly IPdfAnnotationPreviewStorage _previewStorage;

        private string? _entryId;
        private string? _pdfPath;
        private string? _pdfHash;
        private System.Uri? _documentSource;
        private PdfAnnotationViewModel? _selectedAnnotation;
        private bool _isBusy;

        public PdfViewerViewModel(
            HookOrchestrator hookOrchestrator,
            IUserContext userContext,
            IPdfAnnotationPreviewStorage previewStorage)
        {
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _previewStorage = previewStorage ?? throw new ArgumentNullException(nameof(previewStorage));

            Annotations = new ObservableCollection<PdfAnnotationViewModel>();

            LoadPdfCommand = new AsyncRelayCommand(LoadPdfAsync, () => !IsBusy);
            RecordAnnotationChangeCommand = new AsyncRelayCommand(RecordAnnotationChangeAsync, CanRecordAnnotationChange);
        }

        /// <summary>
        /// Gets the collection of annotations displayed in the sidebar.
        /// </summary>
        public ObservableCollection<PdfAnnotationViewModel> Annotations { get; }

        /// <summary>
        /// Gets or sets the workspace entry identifier backing the viewer session.
        /// </summary>
        public string? EntryId
        {
            get => _entryId;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (SetProperty(ref _entryId, sanitized))
                {
                    RecordAnnotationChangeCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the PDF file path requested by the user.
        /// </summary>
        public string? PdfPath
        {
            get => _pdfPath;
            set
            {
                var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                SetProperty(ref _pdfPath, sanitized);
            }
        }

        /// <summary>
        /// Gets the SHA-256 hash of the currently loaded PDF file.
        /// </summary>
        public string? PdfHash
        {
            get => _pdfHash;
            private set => SetProperty(ref _pdfHash, value);
        }

        /// <summary>
        /// Gets the URI provided to the WebView2 control for rendering.
        /// </summary>
        public System.Uri? DocumentSource
        {
            get => _documentSource;
            private set => SetProperty(ref _documentSource, value);
        }

        /// <summary>
        /// Gets or sets the annotation currently selected in the UI.
        /// </summary>
        public PdfAnnotationViewModel? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set
            {
                if (SetProperty(ref _selectedAnnotation, value))
                {
                    RecordAnnotationChangeCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Indicates whether the viewer is performing a background operation.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    LoadPdfCommand.RaiseCanExecuteChanged();
                    RecordAnnotationChangeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Command invoked to load the current <see cref="PdfPath"/> into the viewer.
        /// </summary>
        public IAsyncRelayCommand LoadPdfCommand { get; }

        /// <summary>
        /// Command invoked to persist annotation changes to the changelog hook.
        /// </summary>
        public IAsyncRelayCommand RecordAnnotationChangeCommand { get; }

        private async Task LoadPdfAsync()
        {
            var path = PdfPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                DocumentSource = null;
                PdfHash = null;
                return;
            }

            try
            {
                IsBusy = true;

                var absolutePath = Path.GetFullPath(path);
                if (!File.Exists(absolutePath))
                {
                    DocumentSource = null;
                    PdfHash = null;
                    return;
                }

                await using var stream = new FileStream(absolutePath,
                                                        FileMode.Open,
                                                        FileAccess.Read,
                                                        FileShare.Read,
                                                        bufferSize: 81920,
                                                        useAsync: true);
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(stream, CancellationToken.None).ConfigureAwait(false);
                PdfHash = Convert.ToHexString(hashBytes);
                DocumentSource = new Uri(absolutePath, UriKind.Absolute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DocumentSource = null;
                PdfHash = null;
                Trace.TraceError("Failed to load PDF '{0}': {1}", path, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RecordAnnotationChangeAsync()
        {
            if (string.IsNullOrWhiteSpace(EntryId) || SelectedAnnotation is null)
            {
                return;
            }

            var tags = new List<string>
            {
                "annotationId:" + SelectedAnnotation.Id
            };

            if (!string.IsNullOrWhiteSpace(SelectedAnnotation.Title))
            {
                tags.Add("annotationTitle:" + SelectedAnnotation.Title);
            }

            await ReviewChangeLogWriter.WriteAsync(_hookOrchestrator,
                                                   EntryId,
                                                   _userContext.UserName,
                                                   "annotation-change",
                                                   tags,
                                                   CancellationToken.None).ConfigureAwait(false);
        }

        private bool CanRecordAnnotationChange()
            => !IsBusy && !string.IsNullOrWhiteSpace(EntryId) && SelectedAnnotation is not null;

        public async Task HandleHighlightPreviewAsync(string annotationId, byte[] pngBytes, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(annotationId) || pngBytes is null || pngBytes.Length == 0)
            {
                return;
            }

            var hash = PdfHash;
            if (string.IsNullOrWhiteSpace(hash))
            {
                return;
            }

            _ = width;
            _ = height;

            try
            {
                var relativePath = await _previewStorage.SaveAsync(hash, annotationId, pngBytes, CancellationToken.None).ConfigureAwait(true);

                var annotation = Annotations.FirstOrDefault(a => string.Equals(a.Id, annotationId, StringComparison.OrdinalIgnoreCase));
                if (annotation is not null)
                {
                    annotation.PreviewImagePath = relativePath;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to persist annotation preview: {0}", ex);
            }
        }
    }
}
