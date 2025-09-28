#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class DataExtractionPlaygroundViewModel
{
    private readonly RoiSelectionViewModel _roiSelection = new();
    private System.Windows.Point? _selectionStart;
    private System.Windows.Media.Imaging.RenderTargetBitmap? _currentPageBitmap;
    private int _ocrRegionCounter = 1;

    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapSource? previewBitmap;

    [ObservableProperty]
    private bool isPreviewBusy;

    [ObservableProperty]
    private string previewStatusMessage = "Select a page to render.";

    [ObservableProperty]
    private int? selectedPreviewPage;

    public RoiSelectionViewModel RoiSelection => _roiSelection;

    public bool HasPreview => PreviewBitmap is not null;

    public double PreviewWidth => PreviewBitmap?.PixelWidth ?? 0;

    public double PreviewHeight => PreviewBitmap?.PixelHeight ?? 0;

    partial void InitializePreviewState()
    {
        _roiSelection.PropertyChanged += (_, _) => RunRegionOcrCommand.NotifyCanExecuteChanged();
        PreviewStatusMessage = "Select a page to render.";
    }

    partial void ResetPreviewState()
    {
        _ocrRegionCounter = 1;
        _selectionStart = null;
        _currentPageBitmap = null;
        PreviewBitmap = null;
        SelectedPreviewPage = null;
        PreviewStatusMessage = "Select a page to render.";
        _roiSelection.Clear();
        PreviewPages.Clear();
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    partial void ApplyPreviewPages(IReadOnlyList<int> pages)
    {
        PreviewPages.Clear();
        foreach (var page in pages)
        {
            PreviewPages.Add(page);
        }

        if (PreviewPages.Count > 0)
        {
            SelectedPreviewPage = PreviewPages[0];
        }
        else
        {
            SelectedPreviewPage = null;
            PreviewStatusMessage = "No renderable pages were detected.";
        }
    }

    partial void OnPreviewBitmapChanged(System.Windows.Media.Imaging.BitmapSource? value)
    {
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
        OnPropertyChanged(nameof(HasPreview));
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPreviewPageChanged(int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        _ = LoadPreviewAsync(value.Value);
    }

    partial void OnIsBusyChanged(bool value)
    {
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPreviewBusyChanged(bool value)
    {
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadPreviewAsync(int pageNumber)
    {
        if (_pdfPath is null || IsPreviewBusy)
        {
            return;
        }

        IsPreviewBusy = true;
        PreviewStatusMessage = $"Rendering page {pageNumber}...";
        _roiSelection.Clear();
        _selectionStart = null;

        try
        {
            await Task.Yield();
            using var document = PdfDocument.Open(_pdfPath, new ParsingOptions { ClipPaths = true, UseLenientParsing = true });
            var page = document.GetPage(pageNumber);
            _currentPageBitmap = RenderPageBitmap(page);
            PreviewBitmap = _currentPageBitmap;
            PreviewStatusMessage = $"Preview ready for page {pageNumber}. Drag to select a region.";
        }
        catch (Exception ex)
        {
            PreviewBitmap = null;
            PreviewStatusMessage = $"Failed to render page {pageNumber}.";
            System.Windows.MessageBox.Show(
                $"Failed to render page {pageNumber}:{Environment.NewLine}{ex.Message}",
                "Page preview",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewBusy = false;
            RunRegionOcrCommand.NotifyCanExecuteChanged();
        }
    }

    public void BeginRegionSelection(System.Windows.Point point)
    {
        if (!HasPreview || IsPreviewBusy)
        {
            return;
        }

        var clamped = ClampPoint(point);
        _selectionStart = clamped;
        _roiSelection.Begin(clamped);
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    public void UpdateRegionSelection(System.Windows.Point point)
    {
        if (_selectionStart is null || !_roiSelection.IsSelecting)
        {
            return;
        }

        var rect = NormalizeRect(_selectionStart.Value, ClampPoint(point));
        _roiSelection.Update(rect);
    }

    public void CompleteRegionSelection(System.Windows.Point point)
    {
        if (_selectionStart is null || !_roiSelection.IsSelecting)
        {
            return;
        }

        var rect = NormalizeRect(_selectionStart.Value, ClampPoint(point));
        if (rect.Width < 4 || rect.Height < 4)
        {
            CancelRegionSelection();
            return;
        }

        _roiSelection.Complete(rect);
        _selectionStart = null;
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    public void CancelRegionSelection()
    {
        _selectionStart = null;
        _roiSelection.Clear();
        RunRegionOcrCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearRegionSelection()
    {
        CancelRegionSelection();
    }

    [RelayCommand(CanExecute = nameof(CanRunRegionOcr))]
    private async Task RunRegionOcrAsync()
    {
        if (!RoiSelection.HasSelection || _currentPageBitmap is null || !SelectedPreviewPage.HasValue)
        {
            return;
        }

        var region = RoiSelection.GetSelectionRect();
        var cropped = CreateCrop(region);
        if (cropped is null)
        {
            System.Windows.MessageBox.Show(
                "Selected region is outside the rendered page.",
                "Region OCR",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            IsBusy = true;
            PreviewStatusMessage = "Running OCR on selection...";
            StatusMessage = "Running OCR on selection...";

            var ocrResult = await RecognizeRegionAsync(cropped.Value).ConfigureAwait(true);
            if (ocrResult is null)
            {
                StatusMessage = "OCR returned no content.";
                PreviewStatusMessage = "OCR returned no content.";
                return;
            }

            var regionIndex = _ocrRegionCounter++;
            var table = DataExtractionTableViewModel.FromOcrRegion(
                SelectedPreviewPage.Value,
                regionIndex,
                ocrResult.Rows);

            Tables.Add(table);
            SelectedTable = table;
            RefreshSelectedTableView();
            OnPropertyChanged(nameof(HasResults));
            CopyTableCommand.NotifyCanExecuteChanged();
            StatusMessage = $"OCR region {regionIndex} captured {ocrResult.Rows.Count} rows across {ocrResult.ColumnCount} columns (confidence {ocrResult.Confidence:F0}%).";
            PreviewStatusMessage = "Region OCR complete.";

            await WriteRegionOcrChangeLogAsync(
                SelectedPreviewPage.Value,
                region,
                ocrResult.Rows.Count,
                ocrResult.ColumnCount,
                ocrResult.Confidence).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = "OCR not configured.";
            PreviewStatusMessage = ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "Region OCR",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (DllNotFoundException ex)
        {
            StatusMessage = "OCR runtime missing.";
            PreviewStatusMessage = ex.Message;
            System.Windows.MessageBox.Show(
                $"Failed to load Tesseract runtime:{Environment.NewLine}{ex.Message}",
                "Region OCR",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (TesseractException ex)
        {
            StatusMessage = "OCR failed.";
            PreviewStatusMessage = ex.Message;
            System.Windows.MessageBox.Show(
                $"Tesseract failed to process the selection:{Environment.NewLine}{ex.Message}",
                "Region OCR",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            StatusMessage = "OCR failed.";
            PreviewStatusMessage = "OCR failed.";
            System.Windows.MessageBox.Show(
                $"Unexpected OCR failure:{Environment.NewLine}{ex.Message}",
                "Region OCR",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RunRegionOcrCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunRegionOcr()
    {
        return !IsBusy && !IsPreviewBusy && RoiSelection.HasSelection && PreviewBitmap is not null && SelectedPreviewPage.HasValue;
    }

    private System.Windows.Point ClampPoint(System.Windows.Point point)
    {
        var width = PreviewWidth;
        var height = PreviewHeight;
        var x = Math.Max(0, Math.Min(width, point.X));
        var y = Math.Max(0, Math.Min(height, point.Y));
        return new System.Windows.Point(x, y);
    }

    private static System.Windows.Rect NormalizeRect(System.Windows.Point start, System.Windows.Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new System.Windows.Rect(x, y, width, height);
    }

    private System.Windows.Int32Rect? CreateCrop(System.Windows.Rect rect)
    {
        if (_currentPageBitmap is null)
        {
            return null;
        }

        var x = (int)Math.Max(0, Math.Floor(rect.X));
        var y = (int)Math.Max(0, Math.Floor(rect.Y));
        var width = (int)Math.Min(_currentPageBitmap.PixelWidth - x, Math.Ceiling(rect.Width));
        var height = (int)Math.Min(_currentPageBitmap.PixelHeight - y, Math.Ceiling(rect.Height));

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return new System.Windows.Int32Rect(x, y, width, height);
    }

    private async Task<OcrRegionTableResult?> RecognizeRegionAsync(System.Windows.Int32Rect crop)
    {
        if (_currentPageBitmap is null)
        {
            return null;
        }

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        var cropped = new System.Windows.Media.Imaging.CroppedBitmap(_currentPageBitmap, crop);
        cropped.Freeze();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(cropped));

        await using var memory = new MemoryStream();
        encoder.Save(memory);
        var bytes = memory.ToArray();

        return await Task.Run(() => ExecuteTesseract(bytes)).ConfigureAwait(false);
    }

    private OcrRegionTableResult? ExecuteTesseract(byte[] image)
    {
        var tessData = ResolveTessDataDirectory();
        if (tessData is null)
        {
            throw new InvalidOperationException(
                "Tesseract data directory was not found. Configure 'tessdata' under the workspace or set TESSDATA_PREFIX.");
        }

        using var engine = new TesseractEngine(tessData, "eng", EngineMode.Default);
        using var pix = Pix.LoadFromMemory(image);
        using var page = engine.Process(pix, PageSegMode.Table);

        var tsv = page.GetTsvText();
        var table = TesseractTableBuilder.Build(tsv);
        if (table.ColumnCount == 0)
        {
            var text = page.GetText();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            table = TesseractTableBuilder.FromPlainText(lines);
        }

        if (table.ColumnCount == 0)
        {
            return null;
        }

        var confidence = page.TryGetMeanConfidence(out var conf) ? conf : 0;
        return new OcrRegionTableResult(table.Rows, table.ColumnCount, confidence);
    }

    private static System.Windows.Media.Imaging.RenderTargetBitmap RenderPageBitmap(Page page)
    {
        const double targetDpi = 144d;
        var scale = targetDpi / 72d;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(page.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(page.Height * scale));

        var visual = new System.Windows.Media.DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(0, 0, pixelWidth, pixelHeight));
            var typeface = new System.Windows.Media.Typeface("Segoe UI");

            foreach (var letter in page.Letters)
            {
                if (string.IsNullOrWhiteSpace(letter.Value))
                {
                    continue;
                }

                var fontSize = Math.Max(letter.PointSize * scale, 6);
                var formatted = new System.Windows.Media.FormattedText(
                    letter.Value,
                    CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    System.Windows.Media.Brushes.Black,
                    1.0);

                var x = letter.GlyphRectangle.Left * scale;
                var y = (page.Height - letter.GlyphRectangle.Top) * scale;
                context.DrawText(formatted, new System.Windows.Point(x, y));
            }
        }

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            targetDpi,
            targetDpi,
            System.Windows.Media.PixelFormats.Pbgra32);

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private string? ResolveTessDataDirectory()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX"),
            _workspace.WorkspacePath is null ? null : Path.Combine(_workspace.WorkspacePath, ".knowledgeworks", "tessdata"),
            _workspace.WorkspacePath is null ? null : Path.Combine(_workspace.WorkspacePath, "tessdata"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task WriteRegionOcrChangeLogAsync(int pageNumber,
                                                    System.Windows.Rect selection,
                                                    int rows,
                                                    int columns,
                                                    double confidence)
    {
        if (_entryId is null)
        {
            return;
        }

        var tags = new List<string>
        {
            "mode:OcrRegion",
            $"page:{pageNumber}",
            $"rows:{rows}",
            $"columns:{columns}",
            $"confidence:{confidence.ToString("F0", CultureInfo.InvariantCulture)}",
            $"roi:{selection.X.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Y.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Width.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Height.ToString("F0", CultureInfo.InvariantCulture)}"
        };

        var hook = new HookM.EntryChangeLogHook
        {
            Events = new List<HookM.EntryChangeLogEvent>
            {
                new()
                {
                    EventId = Guid.NewGuid().ToString("N"),
                    TimestampUtc = DateTime.UtcNow,
                    PerformedBy = GetCurrentUser(),
                    Action = "DataExtractionRegionOcr",
                    Details = new HookM.ChangeLogAttachmentDetails
                    {
                        Title = DocumentTitle,
                        LibraryPath = NormalizeLibraryPath(_pdfRelativePath ?? _pdfPath),
                        Purpose = AttachmentKind.Metadata,
                        AttachmentId = _pdfAttachmentId ?? string.Empty,
                        Tags = tags
                    }
                }
            }
        };

        try
        {
            await _hookOrchestrator.ProcessAsync(
                _entryId,
                new HookContext { ChangeLog = hook },
                System.Threading.CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Ignore hook failures.
        }
    }

    private sealed record OcrRegionTableResult(IReadOnlyList<IReadOnlyList<string>> Rows, int ColumnCount, double Confidence);
}
