#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using Tesseract;
using Tabula;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class DataExtractionPlaygroundViewModel
{
    private readonly RoiSelectionViewModel _roiSelection = new();
    private System.Windows.Point? _selectionStart;
    private System.Windows.Media.Imaging.BitmapSource? _currentPageBitmap;
    private double _currentPageWidthPoints;
    private double _currentPageHeightPoints;
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
        _roiSelection.PropertyChanged += (_, _) =>
        {
            RunRegionOcrCommand.NotifyCanExecuteChanged();
            ExtractRegionTablesCommand.NotifyCanExecuteChanged();
        };
        PreviewStatusMessage = "Select a page to render.";
    }

    partial void ResetPreviewState()
    {
        _ocrRegionCounter = 1;
        _selectionStart = null;
        _currentPageBitmap = null;
        _currentPageWidthPoints = 0d;
        _currentPageHeightPoints = 0d;
        PreviewBitmap = null;
        SelectedPreviewPage = null;
        PreviewStatusMessage = "Select a page to render.";
        _roiSelection.Clear();
        PreviewPages.Clear();
        RunRegionOcrCommand.NotifyCanExecuteChanged();
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
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
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
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
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPreviewBusyChanged(bool value)
    {
        RunRegionOcrCommand.NotifyCanExecuteChanged();
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
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
            _currentPageWidthPoints = page.Width;
            _currentPageHeightPoints = page.Height;
            _currentPageBitmap = RenderPageBitmap(_pdfPath, pageNumber, page);
            PreviewBitmap = _currentPageBitmap;
            PreviewStatusMessage = $"Preview ready for page {pageNumber}. Drag to select a region.";
        }
        catch (Exception ex)
        {
            PreviewBitmap = null;
            _currentPageWidthPoints = 0d;
            _currentPageHeightPoints = 0d;
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
            ExtractRegionTablesCommand.NotifyCanExecuteChanged();
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
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
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
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
    }

    public void CancelRegionSelection()
    {
        _selectionStart = null;
        _roiSelection.Clear();
        RunRegionOcrCommand.NotifyCanExecuteChanged();
        ExtractRegionTablesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearRegionSelection()
    {
        CancelRegionSelection();
    }

    [RelayCommand(CanExecute = nameof(CanExtractRegionTables))]
    private async Task ExtractRegionTablesAsync()
    {
        if (_pdfPath is null || !RoiSelection.HasSelection || _currentPageBitmap is null || !SelectedPreviewPage.HasValue)
        {
            return;
        }

        var selection = RoiSelection.GetSelectionRect();
        var pdfRectangle = BuildPdfRectangle(selection);
        if (pdfRectangle is null)
        {
            System.Windows.MessageBox.Show(
                "Selected region could not be mapped to the PDF coordinates.",
                "Region extraction",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var pageNumber = SelectedPreviewPage.Value;

        try
        {
            IsBusy = true;
            StatusMessage = "Extracting tables from selection...";
            PreviewStatusMessage = $"Extracting selection on page {pageNumber}...";

            using var document = PdfDocument.Open(_pdfPath, new ParsingOptions { ClipPaths = true });
            var page = ObjectExtractor.Extract(document, pageNumber);
            var extraction = CreateAlgorithm(SelectedMode.Mode);
            var tableRectangle = new TableRectangle(pdfRectangle.Value);
            var area = page.GetArea(tableRectangle.BoundingBox);
            var extracted = extraction.Extract(area);

            var newTables = new List<DataExtractionTableViewModel>();
            if (extracted is not null)
            {
                var nextIndex = Tables
                    .Where(table => table.PageNumber == pageNumber)
                    .Select(table => table.TableIndex)
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                foreach (var table in extracted)
                {
                    if (table is null)
                    {
                        continue;
                    }

                    var viewModel = DataExtractionTableViewModel.FromTable(
                        pageNumber,
                        nextIndex++,
                        SelectedMode.Mode,
                        TableDetectionStrategy.None,
                        table,
                        tableRectangle);
                    newTables.Add(viewModel);
                    Tables.Add(viewModel);
                }
            }

            if (newTables.Count == 0)
            {
                StatusMessage = "No tables detected in the selected region.";
                PreviewStatusMessage = "No tables detected in the selected region.";
            }
            else
            {
                SelectedTable = newTables[0];
                CopyTableCommand.NotifyCanExecuteChanged();
                StatusMessage = $"Extracted {newTables.Count} table(s) from selection on page {pageNumber}.";
                PreviewStatusMessage = "Region extraction complete.";
            }

            OnPropertyChanged(nameof(HasResults));

            await WriteRegionExtractionChangeLogAsync(
                pageNumber,
                selection,
                pdfRectangle.Value,
                newTables.Count,
                newTables.Sum(table => table.RowCount)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Region extraction failed.";
            PreviewStatusMessage = "Region extraction failed.";
            System.Windows.MessageBox.Show(
                $"Failed to extract tables from the selected region:{Environment.NewLine}{ex.Message}",
                "Region extraction",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ExtractRegionTablesCommand.NotifyCanExecuteChanged();
            CopyTableCommand.NotifyCanExecuteChanged();
            RunRegionOcrCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExtractRegionTables()
    {
        return !IsBusy && !IsPreviewBusy && RoiSelection.HasSelection && PreviewBitmap is not null && SelectedPreviewPage.HasValue;
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
                ocrResult.Confidence).ConfigureAwait(true);
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

    private PdfRectangle? BuildPdfRectangle(System.Windows.Rect selection)
    {
        if (_currentPageBitmap is null || _currentPageWidthPoints <= 0d || _currentPageHeightPoints <= 0d)
        {
            return null;
        }

        var pixelWidth = _currentPageBitmap.PixelWidth;
        var pixelHeight = _currentPageBitmap.PixelHeight;
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return null;
        }

        var scaleX = _currentPageWidthPoints / pixelWidth;
        var scaleY = _currentPageHeightPoints / pixelHeight;

        var leftPixels = Math.Max(0d, Math.Min(selection.X, pixelWidth));
        var rightPixels = Math.Max(0d, Math.Min(selection.X + selection.Width, pixelWidth));
        var topPixels = Math.Max(0d, Math.Min(selection.Y, pixelHeight));
        var bottomPixels = Math.Max(0d, Math.Min(selection.Y + selection.Height, pixelHeight));

        var left = leftPixels * scaleX;
        var right = rightPixels * scaleX;
        var top = (_currentPageBitmap.PixelHeight - topPixels) * scaleY;
        var bottom = (_currentPageBitmap.PixelHeight - bottomPixels) * scaleY;

        left = Math.Max(0d, Math.Min(left, _currentPageWidthPoints));
        right = Math.Max(0d, Math.Min(right, _currentPageWidthPoints));
        top = Math.Max(0d, Math.Min(top, _currentPageHeightPoints));
        bottom = Math.Max(0d, Math.Min(bottom, _currentPageHeightPoints));

        if (right - left <= double.Epsilon || top - bottom <= double.Epsilon)
        {
            return null;
        }

        if (bottom > top)
        {
            (bottom, top) = (top, bottom);
        }

        return new PdfRectangle(left, bottom, right, top);
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
        using var page = engine.Process(pix, PageSegMode.SingleBlock);

        var tsv = page.GetTsvText(0);
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

        var confidence = page.GetMeanConfidence() * 100d;
        return new OcrRegionTableResult(table.Rows, table.ColumnCount, confidence);
    }

    private static System.Windows.Media.Imaging.BitmapSource RenderPageBitmap(string pdfPath, int pageNumber, UglyToad.PdfPig.Content.Page page)
    {
        const double targetDpi = 144d;
        var scale = targetDpi / 72d;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(page.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(page.Height * scale));

        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdfPath, new Docnet.Core.Models.PageDimensions(pixelWidth, pixelHeight));
        var pageIndex = Math.Max(0, pageNumber - 1);
        using var pageReader = reader.GetPageReader(pageIndex);
        var raw = pageReader.GetImage();
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();

        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            width,
            height,
            targetDpi,
            targetDpi,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            raw,
            width * 4);

        bitmap.Freeze();
        return bitmap;
    }

    private string? ResolveTessDataDirectory()
    {
        return TessDataLocator.Resolve(_workspace.WorkspacePath);
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

    private async Task WriteRegionExtractionChangeLogAsync(int pageNumber,
                                                           System.Windows.Rect selection,
                                                           PdfRectangle rectangle,
                                                           int tableCount,
                                                           int totalRows)
    {
        if (_entryId is null)
        {
            return;
        }

        var tags = new List<string>
        {
            $"mode:{SelectedMode.Mode}",
            $"detector:{SelectedDetector.Strategy}",
            $"page:{pageNumber}",
            $"tables:{tableCount}",
            $"rows:{totalRows}",
            $"roiPx:{selection.X.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Y.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Width.ToString("F0", CultureInfo.InvariantCulture)}-{selection.Height.ToString("F0", CultureInfo.InvariantCulture)}",
            $"roiPts:{rectangle.Left.ToString("F1", CultureInfo.InvariantCulture)}-{rectangle.Bottom.ToString("F1", CultureInfo.InvariantCulture)}-{rectangle.Right.ToString("F1", CultureInfo.InvariantCulture)}-{rectangle.Top.ToString("F1", CultureInfo.InvariantCulture)}"
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
                    Action = "DataExtractionRegionTabula",
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
