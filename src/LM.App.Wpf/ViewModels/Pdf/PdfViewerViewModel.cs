using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    internal sealed partial class PdfViewerViewModel : ViewModelBase
    {
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly IUserContext _userContext;
        private readonly IPdfAnnotationPreviewStorage _previewStorage;
        private readonly IWorkSpaceService _workspace;
        private readonly IClipboardService _clipboard;

        private string? _entryId;
        private string? _pdfPath;
        private string? _pdfHash;
        private string? _precomputedHash;
        private System.Uri? _documentSource;
        private PdfAnnotation? _selectedAnnotation;
        private bool _isBusy;
        private readonly RelayCommand _copyAnnotationCommand;
        private readonly RelayCommand _deleteAnnotationCommand;
        private readonly RelayCommand _changeAnnotationColorCommand;

        private IPdfWebViewBridge? _webViewBridge;

        public PdfViewerViewModel(
            HookOrchestrator hookOrchestrator,
            IUserContext userContext,
            IPdfAnnotationPreviewStorage previewStorage,
            IWorkSpaceService workspace,
            IClipboardService clipboard)
        {
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _previewStorage = previewStorage ?? throw new ArgumentNullException(nameof(previewStorage));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

            Annotations = new ObservableCollection<PdfAnnotation>();
            Annotations.CollectionChanged += OnAnnotationsCollectionChanged;

            _copyAnnotationCommand = new RelayCommand(OnCopyAnnotation, CanInteractWithAnnotation);
            _deleteAnnotationCommand = new RelayCommand(OnDeleteAnnotation, CanInteractWithAnnotation);
            _changeAnnotationColorCommand = new RelayCommand(OnChangeAnnotationColor, CanChangeAnnotationColor);

            LoadPdfCommand = new AsyncRelayCommand(LoadPdfCoreAsync, () => !IsBusy);
            RecordAnnotationChangeCommand = new AsyncRelayCommand(RecordAnnotationChangeAsync, CanRecordAnnotationChange);
        }

        internal void InitializeContext(string entryId, string pdfPath, string? pdfHash)
        {
            EntryId = entryId;
            PdfPath = pdfPath;

            if (string.IsNullOrWhiteSpace(pdfHash))
            {
                _precomputedHash = null;
                return;
            }

            var normalized = pdfHash.Trim().ToLowerInvariant();
            _precomputedHash = normalized;
            PdfHash = normalized;
        }

        /// <summary>
        /// Gets the collection of annotations displayed in the sidebar.
        /// </summary>
        public ObservableCollection<PdfAnnotation> Annotations { get; }

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
        public PdfAnnotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set
            {
                if (SetProperty(ref _selectedAnnotation, value))
                {
                    RecordAnnotationChangeCommand?.RaiseCanExecuteChanged();
                    _copyAnnotationCommand.RaiseCanExecuteChanged();
                    _deleteAnnotationCommand.RaiseCanExecuteChanged();
                    _changeAnnotationColorCommand.RaiseCanExecuteChanged();
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
                    _copyAnnotationCommand.RaiseCanExecuteChanged();
                    _deleteAnnotationCommand.RaiseCanExecuteChanged();
                    _changeAnnotationColorCommand.RaiseCanExecuteChanged();
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

        /// <summary>
        /// Gets the command used to copy annotation content to the clipboard.
        /// </summary>
        public System.Windows.Input.ICommand CopyAnnotationCommand => _copyAnnotationCommand;

        /// <summary>
        /// Gets the command used to delete an annotation.
        /// </summary>
        public System.Windows.Input.ICommand DeleteAnnotationCommand => _deleteAnnotationCommand;

        /// <summary>
        /// Gets the command used to change the annotation color.
        /// </summary>
        public System.Windows.Input.ICommand ChangeAnnotationColorCommand => _changeAnnotationColorCommand;

        /// <summary>
        /// Gets or sets the bridge used to communicate with the WebView instance.
        /// </summary>
        public IPdfWebViewBridge? WebViewBridge
        {
            private get => _webViewBridge;
            set => _webViewBridge = value;
        }

        public async Task HandleAnnotationSelectionAsync(PdfAnnotation? annotation, CancellationToken cancellationToken)
        {
            SelectedAnnotation = annotation;

            if (annotation is null || string.IsNullOrWhiteSpace(annotation.Id))
            {
                return;
            }

            var bridge = _webViewBridge;
            if (bridge is null)
            {
                return;
            }

            try
            {
                await bridge.ScrollToAnnotationAsync(annotation.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Intentionally ignored.
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to request annotation scroll for '{0}': {1}", annotation.Id, ex);
            }
        }

        private async Task LoadPdfCoreAsync()
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

                if (!string.IsNullOrWhiteSpace(_precomputedHash))
                {
                    PdfHash = _precomputedHash;
                }
                else
                {
                    await using var stream = new FileStream(absolutePath,
                                                            FileMode.Open,
                                                            FileAccess.Read,
                                                            FileShare.Read,
                                                            bufferSize: 81920,
                                                            useAsync: true);
                    using var sha = SHA256.Create();
                    var hashBytes = await sha.ComputeHashAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    PdfHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }

                _precomputedHash = null;
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

            await WriteAnnotationChangeAsync(SelectedAnnotation, "manual-log", CancellationToken.None).ConfigureAwait(false);
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
                    annotation.PreviewImage = TryLoadPreviewBitmap(relativePath);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to persist annotation preview: {0}", ex);
            }
        }

        private bool CanInteractWithAnnotation(object? parameter)
        {
            return !IsBusy && parameter is PdfAnnotation annotation && !string.IsNullOrWhiteSpace(annotation.Id);
        }

        private bool CanChangeAnnotationColor(object? parameter)
        {
            if (IsBusy)
            {
                return false;
            }

            if (parameter is not PdfAnnotationColorCommandParameter request)
            {
                return false;
            }

            return request.Annotation is not null && !string.IsNullOrWhiteSpace(request.Annotation.Id);
        }

        private void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is PdfAnnotation oldAnnotation)
                    {
                        oldAnnotation.PropertyChanged -= OnAnnotationPropertyChanged;
                    }
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is PdfAnnotation newAnnotation)
                    {
                        newAnnotation.PropertyChanged += OnAnnotationPropertyChanged;
                    }
                }
            }
        }

        private void OnAnnotationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PdfAnnotation annotation)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(PdfAnnotation.Note), StringComparison.Ordinal))
            {
                _ = WriteAnnotationChangeAsync(annotation, "note-updated", CancellationToken.None);
            }
        }

        private void OnCopyAnnotation(object? parameter)
        {
            if (parameter is not PdfAnnotation annotation)
            {
                return;
            }

            var text = annotation.TextSnippet;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = annotation.Title;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                _clipboard.SetText(text);
                _ = WriteAnnotationChangeAsync(annotation, "copied", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to copy annotation '{0}': {1}", annotation.Id, ex);
            }
        }

        private void OnDeleteAnnotation(object? parameter)
        {
            if (parameter is not PdfAnnotation annotation)
            {
                return;
            }

            if (!Annotations.Remove(annotation))
            {
                return;
            }

            if (ReferenceEquals(SelectedAnnotation, annotation))
            {
                SelectedAnnotation = null;
            }

            _ = WriteAnnotationChangeAsync(annotation, "deleted", CancellationToken.None);
        }

        private void OnChangeAnnotationColor(object? parameter)
        {
            if (parameter is not PdfAnnotationColorCommandParameter request || request.Annotation is null)
            {
                return;
            }

            request.Annotation.ColorName = string.IsNullOrWhiteSpace(request.ColorName) ? null : request.ColorName.Trim();
            _ = WriteAnnotationChangeAsync(request.Annotation, "color-updated", CancellationToken.None);
        }

        private async Task WriteAnnotationChangeAsync(PdfAnnotation annotation, string? actionTag, CancellationToken cancellationToken)
        {
            if (annotation is null || string.IsNullOrWhiteSpace(annotation.Id) || string.IsNullOrWhiteSpace(EntryId))
            {
                return;
            }

            try
            {
                var tags = new List<string>
                {
                    "annotationId:" + annotation.Id
                };

                if (!string.IsNullOrWhiteSpace(annotation.Title))
                {
                    tags.Add("annotationTitle:" + annotation.Title);
                }

                if (!string.IsNullOrWhiteSpace(actionTag))
                {
                    tags.Add("action:" + actionTag);
                }

                await ReviewChangeLogWriter.WriteAsync(_hookOrchestrator,
                                                       EntryId!,
                                                       _userContext.UserName,
                                                       "annotation-change",
                                                       tags,
                                                       cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Intentionally ignored.
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to write annotation change for '{0}': {1}", annotation.Id, ex);
            }
        }

        private System.Windows.Media.Imaging.BitmapImage? TryLoadPreviewBitmap(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            try
            {
                var absolutePath = _workspace.GetAbsolutePath(relativePath);
                if (!File.Exists(absolutePath))
                {
                    return null;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to load annotation preview '{0}': {1}", relativePath, ex);
                return null;
            }
        }
    }
}
