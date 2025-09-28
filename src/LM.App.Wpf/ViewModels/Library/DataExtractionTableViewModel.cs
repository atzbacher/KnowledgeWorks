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

        var columnCount = Math.Max(table.ColumnCount, GetMaxRowWidth(table.Rows));
        var cells = NormalizeCells(table.Rows, columnCount);
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

    private static IReadOnlyList<IReadOnlyList<string>> NormalizeCells(IReadOnlyList<IReadOnlyList<Cell>> source, int columnCount)
    {
        var rows = new List<IReadOnlyList<string>>();
        if (source is null)
            return rows;

        foreach (var row in source)
        {
            if (row is null)
            {
                rows.Add(CreateEmptyRow(columnCount));
                continue;
            }

            var values = new List<string>(Math.Max(columnCount, row.Count));
            foreach (var cell in row)
            {
                if (cell is null)
                {
                    values.Add(string.Empty);
                    continue;
                }

                var text = cell.GetText(true) ?? string.Empty;
                values.Add(text.Trim());
            }

            while (values.Count < columnCount)
            {
                values.Add(string.Empty);
            }

            rows.Add(values);
        }

        return rows;
    }

    private static DataTable BuildDataTable(IReadOnlyList<IReadOnlyList<string>> rows, int columnCount)
    {
        var table = new DataTable();

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

    private static int GetMaxRowWidth(IReadOnlyList<IReadOnlyList<Cell>> source)
    {
        if (source is null || source.Count == 0)
            return 0;

        var max = 0;
        foreach (var row in source)
        {
            if (row is null)
            {
                continue;
            }

            max = Math.Max(max, row.Count);
        }

        return max;
    }

    private static IReadOnlyList<string> CreateEmptyRow(int columnCount)
    {
        if (columnCount <= 0)
            return Array.Empty<string>();

        var values = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            values[i] = string.Empty;
        }

        return values;
    }

    private static string BuildPreview(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
            return "(empty table)";

        var firstRow = rows[0];
        var columns = firstRow.Select(cell => string.IsNullOrWhiteSpace(cell) ? "(blank)" : cell.Trim());
        return string.Join(" | ", columns);
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
