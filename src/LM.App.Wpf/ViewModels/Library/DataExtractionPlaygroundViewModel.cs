using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;
using Tabula;
using Tabula.Detectors;
using Tabula.Extractors;
using UglyToad.PdfPig;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class DataExtractionPlaygroundViewModel : ViewModelBase
{
    private readonly HookOrchestrator _hookOrchestrator;
    private readonly IWorkSpaceService _workspace;
    private readonly IClipboardService _clipboard;

    private string? _entryId;
    private string? _pdfPath;
    private string? _pdfRelativePath;
    private string? _pdfAttachmentId;

    public DataExtractionPlaygroundViewModel(HookOrchestrator hookOrchestrator,
                                             IWorkSpaceService workspace,
                                             IClipboardService clipboard)
    {
        _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        ModeOptions = new[]
        {
            new ExtractionModeOption(DataExtractionMode.Stream, "Stream (BasicExtractionAlgorithm)", "Optimised for tables without ruled lines."),
            new ExtractionModeOption(DataExtractionMode.Lattice, "Lattice (SpreadsheetExtractionAlgorithm)", "Optimised for ruled tables with clear cell borders.")
        };

        DetectorOptions = new[]
        {
            new DetectorOption(TableDetectionStrategy.Auto, "Auto-detect per mode", "Use Nurminen or Spreadsheet detection depending on the extraction mode."),
            new DetectorOption(TableDetectionStrategy.Nurminen, "Nurminen detector", "Use SimpleNurminenDetectionAlgorithm for table candidates."),
            new DetectorOption(TableDetectionStrategy.Spreadsheet, "Spreadsheet detector", "Use SpreadsheetDetectionAlgorithm (line-based)."),
            new DetectorOption(TableDetectionStrategy.None, "Full page", "Run extraction against the full page bounds.")
        };

        SelectedMode = ModeOptions[0];
        SelectedDetector = DetectorOptions[0];
        PageSelection = "1";

        Tables = new ObservableCollection<DataExtractionTableViewModel>();
    }

    public IReadOnlyList<ExtractionModeOption> ModeOptions { get; }

    public IReadOnlyList<DetectorOption> DetectorOptions { get; }

    [ObservableProperty]
    private ExtractionModeOption selectedMode;

    [ObservableProperty]
    private DetectorOption selectedDetector;

    [ObservableProperty]
    private string documentTitle = string.Empty;

    [ObservableProperty]
    private string pdfFileName = string.Empty;

    [ObservableProperty]
    private Uri? pdfSource;

    [ObservableProperty]
    private string pageSelection;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private DataExtractionTableViewModel? selectedTable;

    public ObservableCollection<DataExtractionTableViewModel> Tables { get; }

    public bool HasResults => Tables.Count > 0;

    public bool HasPdf => PdfSource is not null;

    public bool CanCopyTable => SelectedTable is not null;

    public async Task<bool> InitializeAsync(Entry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ResetState();

        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            System.Windows.MessageBox.Show(
                "Entry is missing an identifier.",
                "Data extraction",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        var pdfSource = ResolvePdfSource(entry);
        if (pdfSource is null)
        {
            System.Windows.MessageBox.Show(
                "Data extraction playground is only available for entries with a PDF file.",
                "Data extraction",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return false;
        }

        await Task.Yield();

        _entryId = entry.Id;
        _pdfPath = pdfSource.AbsolutePath;
        _pdfRelativePath = pdfSource.RelativePath;
        _pdfAttachmentId = pdfSource.AttachmentId;

        DocumentTitle = ResolveEntryTitle(entry);
        PdfFileName = pdfSource.DisplayName;
        PdfSource = new Uri(pdfSource.AbsolutePath);

        var pageIndexes = ReadPageIndexes(pdfSource.AbsolutePath);
        if (pageIndexes.Count > 0)
        {
            PageSelection = pageIndexes[0].ToString();
        }

        StatusMessage = "Configure options and select Extract tables to begin.";
        Tables.Clear();
        SelectedTable = null;

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasPdf));
        OnPropertyChanged(nameof(CanCopyTable));

        ExtractTablesCommand.NotifyCanExecuteChanged();
        CopyTableCommand.NotifyCanExecuteChanged();

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExtractTables))]
    private async Task ExtractTablesAsync()
    {
        if (_pdfPath is null)
            return;

        IsBusy = true;
        StatusMessage = "Extracting tables...";
        Tables.Clear();
        SelectedTable = null;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(CanCopyTable));

        try
        {
            using var document = PdfDocument.Open(_pdfPath, new ParsingOptions { ClipPaths = true });
            var pageNumbers = ParsePages(PageSelection, document.NumberOfPages);
            if (pageNumbers.Count == 0)
            {
                StatusMessage = "No valid pages were selected.";
                return;
            }

            var mode = SelectedMode.Mode;
            var detector = SelectedDetector.Strategy;
            var totalTables = 0;

            foreach (var pageNumber in pageNumbers)
            {
                var page = ObjectExtractor.Extract(document, pageNumber);
                var tables = ExtractFromPage(page, mode, detector);
                foreach (var table in tables)
                {
                    Tables.Add(table);
                    totalTables++;
                }
            }

            if (Tables.Count == 0)
            {
                StatusMessage = "No tables were detected with the current settings.";
            }
            else
            {
                StatusMessage = $"Extracted {Tables.Count} tables.";
                SelectedTable = Tables[0];
                OnPropertyChanged(nameof(CanCopyTable));
            }

            await WriteChangeLogEventAsync(mode, detector, pageNumbers, totalTables);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Extraction failed.";
            System.Windows.MessageBox.Show(
                $"Failed to extract tables:\n{ex.Message}",
                "Data extraction",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ExtractTablesCommand.NotifyCanExecuteChanged();
            CopyTableCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasResults));
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyTable))]
    private void CopyTable()
    {
        var table = SelectedTable;
        if (table is null)
            return;

        try
        {
            _clipboard.SetText(table.ToTsv());
            StatusMessage = $"Copied table from page {table.PageNumber} to the clipboard.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to copy table:\n{ex.Message}",
                "Copy table",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    partial void OnSelectedTableChanged(DataExtractionTableViewModel? value)
    {
        CopyTableCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanCopyTable));
    }

    private bool CanExtractTables()
    {
        return !IsBusy && _pdfPath is not null;
    }

    private static string ResolveEntryTitle(Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            return entry.DisplayName!.Trim();
        if (!string.IsNullOrWhiteSpace(entry.Title))
            return entry.Title!.Trim();
        return entry.Id ?? "Entry";
    }

    private static IReadOnlyList<int> ReadPageIndexes(string pdfPath)
    {
        try
        {
            using var document = PdfDocument.Open(pdfPath);
            return Enumerable.Range(1, document.NumberOfPages).ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private IReadOnlyList<int> ParsePages(string? input, int totalPages)
    {
        var pages = new SortedSet<int>();
        var maxPage = Math.Max(totalPages, 1);

        if (string.IsNullOrWhiteSpace(input))
        {
            pages.Add(1);
            return pages.ToList();
        }

        var segments = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 1; i <= maxPage; i++)
                    pages.Add(i);
                continue;
            }

            var dashIndex = trimmed.IndexOf('-');
            if (dashIndex > 0)
            {
                if (int.TryParse(trimmed[..dashIndex], out var start) &&
                    int.TryParse(trimmed[(dashIndex + 1)..], out var end))
                {
                    if (start > end)
                        (start, end) = (end, start);

                    start = Math.Max(1, Math.Min(maxPage, start));
                    end = Math.Max(1, Math.Min(maxPage, end));

                    for (var i = start; i <= end; i++)
                        pages.Add(i);
                }
                continue;
            }

            if (int.TryParse(trimmed, out var pageNumber))
            {
                pageNumber = Math.Max(1, Math.Min(maxPage, pageNumber));
                pages.Add(pageNumber);
            }
        }

        return pages.ToList();
    }

    private IReadOnlyList<DataExtractionTableViewModel> ExtractFromPage(PageArea page,
                                                                        DataExtractionMode mode,
                                                                        TableDetectionStrategy detector)
    {
        var extraction = CreateAlgorithm(mode);
        var regions = DetectRegions(page, detector, mode);
        var results = new List<DataExtractionTableViewModel>();
        var index = 1;

        if (regions.Count == 0)
        {
            var extracted = extraction.Extract(page);
            foreach (var table in extracted)
            {
                results.Add(DataExtractionTableViewModel.FromTable(page.PageNumber, index++, mode, detector, table, null));
            }

            return results;
        }

        foreach (var region in regions)
        {
            var area = page.GetArea(region.BoundingBox);
            var extracted = extraction.Extract(area);
            foreach (var table in extracted)
            {
                results.Add(DataExtractionTableViewModel.FromTable(page.PageNumber, index++, mode, detector, table, region));
            }
        }

        return results;
    }

    private static IExtractionAlgorithm CreateAlgorithm(DataExtractionMode mode)
    {
        return mode switch
        {
            DataExtractionMode.Lattice => new SpreadsheetExtractionAlgorithm(),
            _ => new BasicExtractionAlgorithm()
        };
    }

    private static IReadOnlyList<TableRectangle> DetectRegions(PageArea page,
                                                               TableDetectionStrategy strategy,
                                                               DataExtractionMode mode)
    {
        return strategy switch
        {
            TableDetectionStrategy.Nurminen => new SimpleNurminenDetectionAlgorithm().Detect(page),
            TableDetectionStrategy.Spreadsheet => new SpreadsheetDetectionAlgorithm().Detect(page),
            TableDetectionStrategy.Auto => mode == DataExtractionMode.Lattice
                ? new SpreadsheetDetectionAlgorithm().Detect(page)
                : new SimpleNurminenDetectionAlgorithm().Detect(page),
            _ => Array.Empty<TableRectangle>()
        };
    }

    private async Task WriteChangeLogEventAsync(DataExtractionMode mode,
                                                TableDetectionStrategy detector,
                                                IReadOnlyCollection<int> pages,
                                                int tableCount)
    {
        if (_entryId is null)
            return;

        var tags = new List<string>
        {
            $"mode:{mode}",
            $"detector:{detector}",
            $"pages:{string.Join('-', CompressSequence(pages))}",
            $"tables:{tableCount}"
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
                    Action = "DataExtractionRun",
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
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Logging best-effort; ignore failures.
        }
    }

    private static IEnumerable<string> CompressSequence(IReadOnlyCollection<int> pages)
    {
        if (pages.Count == 0)
            yield break;

        var ordered = pages.OrderBy(p => p).ToArray();
        var start = ordered[0];
        var end = start;

        for (var i = 1; i < ordered.Length; i++)
        {
            var current = ordered[i];
            if (current == end + 1)
            {
                end = current;
                continue;
            }

            yield return start == end ? start.ToString() : $"{start}-{end}";
            start = end = current;
        }

        yield return start == end ? start.ToString() : $"{start}-{end}";
    }

    private static string GetCurrentUser()
    {
        var user = Environment.UserName;
        return string.IsNullOrWhiteSpace(user) ? "unknown" : user.Trim();
    }

    private static string NormalizeLibraryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Replace("\\", "/");
    }

    private void ResetState()
    {
        Tables.Clear();
        SelectedTable = null;
        _entryId = null;
        _pdfPath = null;
        _pdfRelativePath = null;
        _pdfAttachmentId = null;
        PdfSource = null;
        DocumentTitle = string.Empty;
        PdfFileName = string.Empty;
        PageSelection = string.Empty;
        StatusMessage = string.Empty;

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasPdf));
        OnPropertyChanged(nameof(CanCopyTable));

        ExtractTablesCommand.NotifyCanExecuteChanged();
        CopyTableCommand.NotifyCanExecuteChanged();
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

    private sealed record PdfSourceInfo(string AbsolutePath, string RelativePath, string DisplayName, string? AttachmentId);
}

  internal enum DataExtractionMode
  {
      Stream,
      Lattice
  }

  internal enum TableDetectionStrategy
  {
      Auto,
      Nurminen,
      Spreadsheet,
      None
  }

  internal sealed record ExtractionModeOption(DataExtractionMode Mode, string DisplayName, string Description);

  internal sealed record DetectorOption(TableDetectionStrategy Strategy, string DisplayName, string Description);
