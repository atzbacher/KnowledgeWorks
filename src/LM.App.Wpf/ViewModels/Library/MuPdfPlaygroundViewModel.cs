using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using MuPDFCore;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class MuPdfPlaygroundViewModel : ViewModelBase, IDisposable
{
    private readonly HookOrchestrator _hookOrchestrator;
    private readonly IWorkSpaceService _workspace;
    private readonly IClipboardService _clipboard;
    private readonly ObservableCollection<int> _pageNumbers;
    private readonly ReadOnlyObservableCollection<int> _readonlyPageNumbers;
    private readonly SemaphoreSlim _renderGate = new(1, 1);

    private MuPDFContext? _context;
    private MuPDFDocument? _document;
    private string? _entryId;
    private string? _pdfPath;
    private string? _pdfRelativePath;
    private string? _attachmentId;
    private bool _isInitializing;
    private bool _isDisposed;

    public MuPdfPlaygroundViewModel(HookOrchestrator hookOrchestrator,
                                    IWorkSpaceService workspace,
                                    IClipboardService clipboard)
    {
        _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        _pageNumbers = new ObservableCollection<int>();
        _readonlyPageNumbers = new ReadOnlyObservableCollection<int>(_pageNumbers);
        PageNumbers = _readonlyPageNumbers;

        ZoomOptions = new[] { 50, 75, 100, 125, 150, 200, 300, 400 };
        SelectedZoom = 125;
        SelectedPageNumber = 1;
        IncludePdfAnnotations = true;
        DocumentTitle = string.Empty;
        StatusMessage = string.Empty;

        Annotations = new ObservableCollection<MuPdfAnnotationViewModel>();
        Annotations.CollectionChanged += OnAnnotationsCollectionChanged;
    }

    public ReadOnlyObservableCollection<int> PageNumbers { get; }

    public IReadOnlyList<int> ZoomOptions { get; }

    public ObservableCollection<MuPdfAnnotationViewModel> Annotations { get; }

    [ObservableProperty]
    private string documentTitle;

    [ObservableProperty]
    private int selectedPageNumber;

    [ObservableProperty]
    private int selectedZoom;

    [ObservableProperty]
    private BitmapSource? currentPageImage;

    [ObservableProperty]
    private double currentPagePixelWidth;

    [ObservableProperty]
    private double currentPagePixelHeight;

    [ObservableProperty]
    private bool includePdfAnnotations;

    [ObservableProperty]
    private bool isSelectionModeEnabled = true;

    [ObservableProperty]
    private MuPdfAnnotationViewModel? selectedAnnotation;

    [ObservableProperty]
    private string statusMessage;

    public bool HasAnnotations => Annotations.Count > 0;

    public bool HasSelectedAnnotation => SelectedAnnotation is not null;

    public async Task<bool> InitializeAsync(Entry entry, CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        ResetState();

        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            System.Windows.MessageBox.Show(
                "Entry is missing an identifier.",
                "MuPDF playground",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        var pdfSource = ResolvePdfSource(entry);
        if (pdfSource is null)
        {
            System.Windows.MessageBox.Show(
                "The MuPDF playground requires an entry with a PDF attachment.",
                "MuPDF playground",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return false;
        }

        var source = pdfSource.Value;

        try
        {
            _context = new MuPDFContext(256 * 1024 * 1024);
            _document = new MuPDFDocument(_context, source.AbsolutePath)
            {
                // Rotated pages can be clipped by MuPDF if the page bounds are enforced.
                // Disable clipping so the entire rendered surface is preserved.
                ClipToPageBounds = false
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open the PDF in MuPDF:\n{ex.Message}",
                "MuPDF playground",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            ResetState();
            return false;
        }

        _entryId = entry.Id;
        _pdfPath = source.AbsolutePath;
        _pdfRelativePath = source.RelativePath;
        _attachmentId = source.AttachmentId;

        DocumentTitle = ResolveEntryTitle(entry);

        _isInitializing = true;
        try
        {
            _pageNumbers.Clear();
            var pageCount = _document.Pages.Count;
            if (pageCount == 0)
            {
                StatusMessage = "This PDF does not contain any pages.";
                return false;
            }

            for (var index = 1; index <= pageCount; index++)
            {
                _pageNumbers.Add(index);
            }

            SelectedPageNumber = 1;
            SelectedZoom = 150;
            IncludePdfAnnotations = true;
            StatusMessage = "Drag on the page to capture annotation regions.";
        }
        finally
        {
            _isInitializing = false;
        }

        await RenderCurrentPageAsync().ConfigureAwait(true);
        return true;
    }

    [RelayCommand]
    private async Task AddAnnotationFromSelectionAsync(System.Windows.Rect selection)
    {
        if (selection.Width <= 2d || selection.Height <= 2d)
        {
            return;
        }

        if (_document is null)
        {
            return;
        }

        var pageSize = new System.Windows.Size(CurrentPagePixelWidth, CurrentPagePixelHeight);
        if (pageSize.Width <= 0d || pageSize.Height <= 0d)
        {
            return;
        }

        var normalized = NormalizedRectangle.FromPixels(selection, pageSize);
        var annotation = new MuPdfAnnotationViewModel(SelectedPageNumber - 1, normalized)
        {
            Note = string.Empty
        };

        annotation.UpdatePixelMetrics(pageSize.Width, pageSize.Height);
        Annotations.Add(annotation);
        SelectedAnnotation = annotation;

        await AppendChangeLogAsync("mupdf-playground.annotation-created", annotation).ConfigureAwait(false);
        StatusMessage = $"Added annotation on page {SelectedPageNumber}.";
    }

    [RelayCommand]
    private async Task RemoveAnnotationAsync(MuPdfAnnotationViewModel? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        if (!Annotations.Remove(annotation))
        {
            return;
        }

        SelectedAnnotation = null;
        await AppendChangeLogAsync("mupdf-playground.annotation-removed", annotation).ConfigureAwait(false);
        StatusMessage = "Annotation removed.";
    }

    [RelayCommand]
    private async Task SaveAnnotationNoteAsync(MuPdfAnnotationViewModel? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        await AppendChangeLogAsync("mupdf-playground.annotation-note-updated", annotation).ConfigureAwait(false);
        StatusMessage = "Annotation note stored in changelog.";
    }

    [RelayCommand]
    private void CopyAnnotationSummary(MuPdfAnnotationViewModel? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        var summary = $"MuPDF annotation — page {annotation.PageNumber + 1} @ {annotation.Region.Width:P1} × {annotation.Region.Height:P1}\nNote: {annotation.Note}";
        _clipboard.SetText(summary);
        StatusMessage = "Annotation summary copied to clipboard.";
    }

    [RelayCommand]
    private Task ExportCurrentPageAsync()
    {
        if (_document is null || string.IsNullOrWhiteSpace(_pdfPath))
        {
            return Task.CompletedTask;
        }

        var directory = Path.GetDirectoryName(_pdfPath) ?? _workspace.GetAbsolutePath("exports");
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(_pdfPath) ?? "entry";
        var safeTitle = string.IsNullOrWhiteSpace(DocumentTitle) ? baseName : DocumentTitle;
        var fileName = string.Concat(Path.GetInvalidFileNameChars().Aggregate(safeTitle, (current, invalid) => current.Replace(invalid, '-')), $"_p{SelectedPageNumber}_mupdf.png");
        var targetPath = Path.Combine(directory, fileName);

        try
        {
            _document.SaveImage(SelectedPageNumber - 1, SelectedZoom / 100d, PixelFormats.BGRA, targetPath, RasterOutputFileTypes.PNG, IncludePdfAnnotations);
            StatusMessage = $"Exported page {SelectedPageNumber} to '{targetPath}'.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to export the current page:\n{ex.Message}",
                "MuPDF playground",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CopyPageTextAsync()
    {
        if (_document is null)
        {
            return;
        }

        try
        {
            var text = await Task.Run(() => _document.ExtractText()).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusMessage = "No text content detected in the PDF.";
                return;
            }

            _clipboard.SetText(text);
            StatusMessage = "Full document text copied using MuPDF extraction.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to extract text using MuPDF:\n{ex.Message}",
                "MuPDF playground",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Annotations.CollectionChanged -= OnAnnotationsCollectionChanged;
        _document?.Dispose();
        _context?.Dispose();
        _renderGate.Dispose();
    }

    private async void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAnnotations));

        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove)
        {
            await Task.Yield();
            UpdateAnnotationMetricsForCurrentPage();
        }
    }

    partial void OnSelectedAnnotationChanged(MuPdfAnnotationViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedAnnotation));
    }

    partial void OnSelectedPageNumberChanged(int value)
    {
        if (_isInitializing || _isDisposed)
        {
            return;
        }

        _ = RenderCurrentPageAsync();
    }

    partial void OnSelectedZoomChanged(int value)
    {
        if (_isInitializing || _isDisposed)
        {
            return;
        }

        _ = RenderCurrentPageAsync();
    }

    partial void OnIncludePdfAnnotationsChanged(bool value)
    {
        if (_isInitializing || _isDisposed)
        {
            return;
        }

        _ = RenderCurrentPageAsync();
    }

    private async Task RenderCurrentPageAsync()
    {
        if (_isDisposed || _document is null)
        {
            return;
        }

        try
        {
            await _renderGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        try
        {
            if (_isDisposed || _document is null)
            {
                return;
            }

            var pageIndex = Math.Clamp(SelectedPageNumber - 1, 0, _document.Pages.Count - 1);
            var zoomFactor = SelectedZoom / 100d;
            var includeAnnotations = IncludePdfAnnotations;

            var renderResult = await Task.Run(() => RenderPageInternal(pageIndex, zoomFactor, includeAnnotations)).ConfigureAwait(true);
            if (renderResult is null)
            {
                StatusMessage = "Unable to render the requested page.";
                return;
            }

            CurrentPageImage = renderResult.Value.Bitmap;
            CurrentPagePixelWidth = renderResult.Value.Size.Width;
            CurrentPagePixelHeight = renderResult.Value.Size.Height;
            StatusMessage = $"Page {SelectedPageNumber} of {PageNumbers.Count} — {SelectedZoom}% zoom";
            UpdateAnnotationMetricsForCurrentPage();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Render failed: {ex.Message}";
        }
        finally
        {
            try
            {
                _renderGate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Disposal raced with an in-flight render; nothing to release.
            }
        }
    }

    private PageRenderResult? RenderPageInternal(int pageIndex, double zoomFactor, bool includeAnnotations)
    {
        if (_document is null)
        {
            return null;
        }

        var page = _document.Pages[pageIndex];
        var bounds = page.Bounds;
        var width = Math.Max(1, (int)Math.Round(bounds.Width * zoomFactor));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * zoomFactor));

        var span = _document.Render(pageIndex, zoomFactor, PixelFormats.BGRA, out var disposable, includeAnnotations);
        try
        {
            var buffer = new byte[span.Length];
            span.CopyTo(buffer);

            var dpi = 72.0 * zoomFactor;
            var stride = width * 4;
            var bitmap = BitmapSource.Create(
                width,
                height,
                dpi,
                dpi,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                buffer,
                stride);
            bitmap.Freeze();

            return new PageRenderResult(bitmap, new System.Windows.Size(width, height));
        }
        finally
        {
            disposable?.Dispose();
        }
    }

        private async Task AppendChangeLogAsync(string action, MuPdfAnnotationViewModel annotation)
        {
            if (string.IsNullOrWhiteSpace(_entryId))
            {
                return;
            }

            try
            {
                var tags = new List<string>
                {
                    "mupdf-playground",
                    $"page:{annotation.PageNumber + 1}"
                };

                if (!string.IsNullOrWhiteSpace(annotation.Note))
                {
                    tags.Add($"note:{annotation.Note.Trim()}");
                }

                tags.Add($"region:{annotation.Region.X:0.###},{annotation.Region.Y:0.###},{annotation.Region.Width:0.###},{annotation.Region.Height:0.###}");

                var hook = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new HookM.EntryChangeLogEvent
                        {
                            PerformedBy = string.IsNullOrWhiteSpace(Environment.UserName) ? "unknown" : Environment.UserName,
                            Action = action,
                            Details = new HookM.ChangeLogAttachmentDetails
                            {
                                AttachmentId = _attachmentId ?? string.Empty,
                                Title = DocumentTitle,
                                LibraryPath = _pdfRelativePath ?? string.Empty,
                                Purpose = AttachmentKind.Supplement,
                                Tags = tags
                            }
                        }
                    }
                };

                await _hookOrchestrator.ProcessAsync(
                    _entryId,
                    new HookContext { ChangeLog = hook },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[MuPdfPlaygroundViewModel] Failed to append changelog for '{_entryId}': {ex}");
            }
        }

    private void UpdateAnnotationMetricsForCurrentPage()
    {
        var targetWidth = CurrentPagePixelWidth;
        var targetHeight = CurrentPagePixelHeight;
        if (targetWidth <= 0d || targetHeight <= 0d)
        {
            return;
        }

        foreach (var annotation in Annotations.Where(a => a.PageNumber == SelectedPageNumber - 1))
        {
            annotation.UpdatePixelMetrics(targetWidth, targetHeight);
        }
    }

    private void ResetState()
    {
        _entryId = null;
        _pdfPath = null;
        _pdfRelativePath = null;
        _attachmentId = null;
        _document?.Dispose();
        _document = null;
        _context?.Dispose();
        _context = null;

        _pageNumbers.Clear();
        Annotations.Clear();
        CurrentPageImage = null;
        CurrentPagePixelWidth = 0d;
        CurrentPagePixelHeight = 0d;
        SelectedAnnotation = null;
        StatusMessage = string.Empty;
    }

    private PdfSourceInfo? ResolvePdfSource(Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.MainFilePath))
        {
            var mainAbsolute = _workspace.GetAbsolutePath(entry.MainFilePath);
            if (!string.IsNullOrWhiteSpace(mainAbsolute) &&
                File.Exists(mainAbsolute) &&
                string.Equals(Path.GetExtension(mainAbsolute), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = entry.MainFilePath ?? string.Empty;
                var displayName = Path.GetFileName(mainAbsolute) ?? relativePath;
                return new PdfSourceInfo(mainAbsolute, relativePath, displayName, null);
            }
        }

        if (entry.Attachments is not null)
        {
            foreach (var attachment in entry.Attachments)
            {
                if (attachment is null || string.IsNullOrWhiteSpace(attachment.RelativePath))
                {
                    continue;
                }

                var absolute = _workspace.GetAbsolutePath(attachment.RelativePath);
                if (string.IsNullOrWhiteSpace(absolute) ||
                    !File.Exists(absolute) ||
                    !string.Equals(Path.GetExtension(absolute), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileName(absolute) ?? attachment.RelativePath;
                var displayName = string.IsNullOrWhiteSpace(attachment.Title)
                    ? fileName
                    : $"{attachment.Title.Trim()} ({fileName})";

                return new PdfSourceInfo(absolute, attachment.RelativePath, displayName, attachment.Id);
            }
        }

        return null;
    }

    private static string ResolveEntryTitle(Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Title))
        {
            return entry.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.MainFilePath))
        {
            return Path.GetFileNameWithoutExtension(entry.MainFilePath) ?? "Entry";
        }

        return "Entry";
    }

    private readonly record struct PdfSourceInfo(string AbsolutePath, string RelativePath, string DisplayName, string? AttachmentId);

    private readonly record struct PageRenderResult(BitmapSource Bitmap, System.Windows.Size Size);
}
