using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Tabula;
using Tabula.Detectors;
using UglyToad.PdfPig.Core;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed class DataExtractionTableViewModel
{
    private readonly IReadOnlyList<IReadOnlyList<string>> _cells;
    private readonly int _columnCount;

    private DataExtractionTableViewModel(int pageNumber,
                                         int tableIndex,
                                         DataExtractionMode mode,
                                         TableDetectionStrategy detector,
                                         TableRectangle? region,
                                         IReadOnlyList<IReadOnlyList<string>> cells,
                                         int columnCount,
                                         DataView dataView,
                                         string preview)
    {
        PageNumber = pageNumber;
        TableIndex = tableIndex;
        Mode = mode;
        Detector = detector;
        Region = region;
        _cells = cells;
        _columnCount = columnCount;
        RowsView = dataView ?? throw new ArgumentNullException(nameof(dataView));
        Preview = preview;
    }

    public int PageNumber { get; }

    public int TableIndex { get; }

    public DataExtractionMode Mode { get; }

    public TableDetectionStrategy Detector { get; }

    public TableRectangle? Region { get; }

    public DataView RowsView { get; }

    public int RowCount => _cells.Count;

    public int ColumnCount => _columnCount;

    public string Preview { get; }

    public string RegionSummary => Region is null ? "Full page" : FormatRegion(Region.BoundingBox);

    public string Title => $"Page {PageNumber} · Table {TableIndex}";

    public static DataExtractionTableViewModel FromTable(int pageNumber,
                                                         int tableIndex,
                                                         DataExtractionMode mode,
                                                         TableDetectionStrategy detector,
                                                         Table table,
                                                         TableRectangle? region)
    {
        if (table is null)
            throw new ArgumentNullException(nameof(table));

        var normalized = NormalizeCells(table.Rows, table.ColumnCount);
        var cells = normalized.Rows;
        var columnCount = normalized.ColumnCount;
        var dataTable = BuildDataTable(cells, columnCount);
        var preview = BuildPreview(cells);

        return new DataExtractionTableViewModel(
            pageNumber,
            tableIndex,
            mode,
            detector,
            region,
            cells,
            columnCount,
            dataTable.DefaultView,
            preview);
    }

    public string ToTsv()
    {
        var rows = new List<string>(_cells.Count);
        foreach (var row in _cells)
        {
            var normalized = row.Select(cell => cell?.Replace("\t", "    ").Replace("\r", " ").Replace("\n", " ") ?? string.Empty);
            rows.Add(string.Join('\t', normalized));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, int ColumnCount) NormalizeCells(IReadOnlyList<IReadOnlyList<Cell>> source,
                                                                                               int reportedColumnCount)
    {
        var normalizedRows = new List<IReadOnlyList<NormalizedCell>>();
        if (source is not null)
        {
            foreach (var row in source)
            {
                if (row is null)
                {
                    normalizedRows.Add(Array.Empty<NormalizedCell>());
                    continue;
                }

                var values = new List<NormalizedCell>(row.Count);
                foreach (var cell in row)
                {
                    if (cell is null)
                    {
                        values.Add(NormalizedCell.Empty);
                        continue;
                    }

                    var text = cell.GetText(true) ?? string.Empty;
                    values.Add(new NormalizedCell(text.Trim(), cell.IsPlaceholder));
                }

                normalizedRows.Add(values);
            }
        }

        return ProjectColumns(normalizedRows, reportedColumnCount);
    }

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, int ColumnCount) ProjectColumns(IReadOnlyList<IReadOnlyList<NormalizedCell>> rows,
                                                                                              int reportedColumnCount)
    {
        var maxRowWidth = rows.Count == 0 ? 0 : rows.Max(static r => r.Count);
        var initialColumnCount = Math.Max(reportedColumnCount, maxRowWidth);

        if (initialColumnCount == 0)
        {
            return (Array.Empty<IReadOnlyList<string>>(), 0);
        }

        var keepIndexes = new List<int>(initialColumnCount);
        for (var column = 0; column < initialColumnCount; column++)
        {
            var hasRealCell = false;
            var hasContent = false;

            foreach (var row in rows)
            {
                if (column >= row.Count)
                {
                    continue;
                }

                var cell = row[column];
                if (!cell.IsPlaceholder)
                {
                    hasRealCell = true;
                }

                if (!string.IsNullOrWhiteSpace(cell.Text))
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent || hasRealCell)
            {
                keepIndexes.Add(column);
            }
        }

        if (keepIndexes.Count == 0)
        {
            keepIndexes.Add(0);
        }

        var projectedRows = new List<IReadOnlyList<string>>(rows.Count);
        foreach (var row in rows)
        {
            var values = new List<string>(keepIndexes.Count);
            foreach (var column in keepIndexes)
            {
                if (column < row.Count)
                {
                    values.Add(row[column].Text);
                }
                else
                {
                    values.Add(string.Empty);
                }
            }

            projectedRows.Add(values);
        }

        return (projectedRows, keepIndexes.Count);
    }

    private static DataTable BuildDataTable(IReadOnlyList<IReadOnlyList<string>> rows, int columnCount)
    {
        var table = new DataTable();

        if (columnCount <= 0)
        {
            return table;
        }

        for (var column = 0; column < columnCount; column++)
        {
            table.Columns.Add($"Column {column + 1}");
        }

        foreach (var row in rows)
        {
            var dataRow = table.NewRow();
            for (var column = 0; column < columnCount; column++)
            {
                dataRow[column] = column < row.Count ? row[column] ?? string.Empty : string.Empty;
            }

            table.Rows.Add(dataRow);
        }

        return table;
    }

    private static string BuildPreview(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
            return "(empty table)";

        var firstRowWithContent = rows.FirstOrDefault(static row => row.Any(static cell => !string.IsNullOrWhiteSpace(cell)))
                                    ?? rows[0];
        var columns = firstRowWithContent.Select(static cell => string.IsNullOrWhiteSpace(cell) ? "(blank)" : cell.Trim());
        return string.Join(" | ", columns);
    }

    private sealed record NormalizedCell(string Text, bool IsPlaceholder)
    {
        public static NormalizedCell Empty { get; } = new(string.Empty, true);
    }

    private static string FormatRegion(PdfRectangle rectangle)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "x:{0:0.##}-{1:0.##}, y:{2:0.##}-{3:0.##}",
            rectangle.Left,
            rectangle.Right,
            rectangle.Bottom,
            rectangle.Top);
    }
}
