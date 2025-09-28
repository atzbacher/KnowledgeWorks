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
                                         string preview)
    {
        PageNumber = pageNumber;
        TableIndex = tableIndex;
        Mode = mode;
        Detector = detector;
        Region = region;
        _cells = cells;
        _columnCount = columnCount;
        Preview = preview;
    }

    public int PageNumber { get; }

    public int TableIndex { get; }

    public DataExtractionMode Mode { get; }

    public TableDetectionStrategy Detector { get; }

    public TableRectangle? Region { get; }

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
        var preview = BuildPreview(cells);

        return new DataExtractionTableViewModel(
            pageNumber,
            tableIndex,
            mode,
            detector,
            region,
            cells,
            columnCount,
            preview);
    }

    public string ToTsv()
    {
        return ToTsv(TableAdjustmentOptions.Default);
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

    public string ToTsv(TableAdjustmentOptions options)
    {
        var adjusted = ApplyAdjustments(options ?? TableAdjustmentOptions.Default);
        var rows = new List<string>();

        if (adjusted.Headers.Count > 0)
        {
            rows.Add(string.Join('\t', adjusted.Headers));
        }

        foreach (var row in adjusted.Rows)
        {
            var normalized = row.Select(static cell => cell?.Replace("\t", "    ").Replace("\r", " ").Replace("\n", " ") ?? string.Empty);
            rows.Add(string.Join('\t', normalized));
        }

        return string.Join(Environment.NewLine, rows);
    }

    public DataView BuildView(TableAdjustmentOptions options)
    {
        var adjusted = ApplyAdjustments(options ?? TableAdjustmentOptions.Default);
        var dataTable = new DataTable();

        foreach (var header in adjusted.Headers)
        {
            dataTable.Columns.Add(header);
        }

        foreach (var row in adjusted.Rows)
        {
            var dataRow = dataTable.NewRow();
            for (var column = 0; column < adjusted.Headers.Count; column++)
            {
                dataRow[column] = column < row.Count ? row[column] ?? string.Empty : string.Empty;
            }

            dataTable.Rows.Add(dataRow);
        }

        return dataTable.DefaultView;
    }

    public string GetRowPreview(int index)
    {
        if (index < 0 || index >= _cells.Count)
        {
            return string.Empty;
        }

        var row = _cells[index];
        if (row.Count == 0)
        {
            return "(empty)";
        }

        var preview = row.Select(static cell => string.IsNullOrWhiteSpace(cell) ? "(blank)" : cell.Trim());
        return string.Join(" | ", preview);
    }

    public IReadOnlyList<IReadOnlyList<string>> GetRawRows()
    {
        return _cells;
    }

    private AdjustedTable ApplyAdjustments(TableAdjustmentOptions options)
    {
        var effective = options ?? TableAdjustmentOptions.Default;
        var working = CloneRows(_cells);
        var initialColumnCount = working.Count == 0 ? 0 : working.Max(static row => row.Count);
        var columnsToRemove = new HashSet<int>();

        if (effective.MergeSignColumns)
        {
            MergeSignColumns(working, columnsToRemove, initialColumnCount);
        }

        var trimmed = ProjectRemainingColumns(working, columnsToRemove);

        if (effective.RemoveEmptyColumns)
        {
            trimmed = RemoveEmptyColumns(trimmed);
        }

        if (effective.RemoveEmptyRows)
        {
            trimmed = RemoveEmptyRows(trimmed);
        }

        var columnCount = trimmed.Count == 0 ? 0 : trimmed.Max(static row => row.Count);
        var headers = BuildHeaders(trimmed, columnCount, effective.HeaderRowIndex);
        var dataRows = ExtractDataRows(trimmed, effective.HeaderRowIndex);

        return new AdjustedTable(headers, dataRows);
    }

    private static List<List<string>> CloneRows(IReadOnlyList<IReadOnlyList<string>> source)
    {
        var result = new List<List<string>>(source.Count);
        foreach (var row in source)
        {
            result.Add(row.Select(static cell => cell ?? string.Empty).ToList());
        }

        return result;
    }

    private static void MergeSignColumns(List<List<string>> rows, HashSet<int> columnsToRemove, int columnCount)
    {
        for (var column = 0; column < columnCount; column++)
        {
            if (columnsToRemove.Contains(column))
            {
                continue;
            }

            if (!IsSignColumn(rows, column))
            {
                continue;
            }

            var targetColumn = FindMergeTarget(rows, column, columnCount, columnsToRemove);
            if (targetColumn < 0)
            {
                continue;
            }

            foreach (var row in rows)
            {
                var sign = column < row.Count ? row[column] : string.Empty;
                if (targetColumn >= row.Count)
                {
                    PadRow(row, targetColumn + 1);
                }

                var value = targetColumn < row.Count ? row[targetColumn] : string.Empty;
                row[targetColumn] = CombineSignAndValue(sign, value);
            }

            columnsToRemove.Add(column);
        }
    }

    private static bool IsSignColumn(List<List<string>> rows, int column)
    {
        var hasCandidate = false;
        foreach (var row in rows)
        {
            if (column >= row.Count)
            {
                continue;
            }

            var text = row[column];
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            hasCandidate = true;
            if (!IsSignToken(text))
            {
                return false;
            }
        }

        return hasCandidate;
    }

    private static bool IsSignToken(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        return trimmed switch
        {
            "+" or "-" or "±" or "∓" or "+/-" or "-/+" => true,
            _ => trimmed.All(static ch => IsSignCharacter(ch))
        };
    }

    private static bool IsSignCharacter(char ch)
    {
        return ch is '+' or '-' or '−' or '±' or '∓' or '/' or '／' or ' ' or '<' or '>' or '=' or '≤' or '≥';
    }

    private static int FindMergeTarget(List<List<string>> rows, int signColumn, int totalColumns, HashSet<int> columnsToRemove)
    {
        var next = FindNumericColumn(rows, signColumn + 1, totalColumns, +1, columnsToRemove);
        if (next >= 0)
        {
            return next;
        }

        return FindNumericColumn(rows, signColumn - 1, -1, -1, columnsToRemove);
    }

    private static int FindNumericColumn(List<List<string>> rows, int start, int boundary, int step, HashSet<int> columnsToRemove)
    {
        for (var column = start; column != boundary; column += step)
        {
            if (column < 0)
            {
                break;
            }

            if (columnsToRemove.Contains(column))
            {
                continue;
            }

            var hasNumeric = false;
            foreach (var row in rows)
            {
                if (column >= row.Count)
                {
                    continue;
                }

                var text = row[column];
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (ContainsDigit(text))
                {
                    hasNumeric = true;
                    break;
                }
            }

            if (hasNumeric)
            {
                return column;
            }
        }

        return -1;
    }

    private static bool ContainsDigit(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static void PadRow(List<string> row, int targetLength)
    {
        while (row.Count < targetLength)
        {
            row.Add(string.Empty);
        }
    }

    private static string CombineSignAndValue(string sign, string value)
    {
        var trimmedSign = sign?.Trim() ?? string.Empty;
        var trimmedValue = value?.Trim() ?? string.Empty;

        if (trimmedSign.Length == 0)
        {
            return trimmedValue;
        }

        if (trimmedValue.Length == 0)
        {
            return trimmedSign;
        }

        var separatorNeeded = NeedsSeparator(trimmedSign, trimmedValue);
        return separatorNeeded ? $"{trimmedSign} {trimmedValue}" : trimmedSign + trimmedValue;
    }

    private static bool NeedsSeparator(string sign, string value)
    {
        if (sign.Length == 0)
        {
            return false;
        }

        var last = sign[^1];
        if (last is '+' or '-' or '/' or '±')
        {
            return false;
        }

        return value.Length > 0 && !char.IsWhiteSpace(value[0]);
    }

    private static List<List<string>> ProjectRemainingColumns(List<List<string>> rows, HashSet<int> columnsToRemove)
    {
        if (columnsToRemove.Count == 0)
        {
            return rows.Select(static row => row.ToList()).ToList();
        }

        var projected = new List<List<string>>(rows.Count);
        foreach (var row in rows)
        {
            var values = new List<string>();
            for (var column = 0; column < row.Count; column++)
            {
                if (columnsToRemove.Contains(column))
                {
                    continue;
                }

                values.Add(row[column]);
            }

            projected.Add(values);
        }

        return projected;
    }

    private static List<List<string>> RemoveEmptyColumns(List<List<string>> rows)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var columnCount = rows.Max(static row => row.Count);
        var keep = new List<int>();

        for (var column = 0; column < columnCount; column++)
        {
            var hasContent = false;
            foreach (var row in rows)
            {
                if (column >= row.Count)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row[column]))
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent)
            {
                keep.Add(column);
            }
        }

        if (keep.Count == 0)
        {
            keep.Add(0);
        }

        var projected = new List<List<string>>(rows.Count);
        foreach (var row in rows)
        {
            var values = new List<string>(keep.Count);
            foreach (var column in keep)
            {
                values.Add(column < row.Count ? row[column] : string.Empty);
            }

            projected.Add(values);
        }

        return projected;
    }

    private static List<List<string>> RemoveEmptyRows(List<List<string>> rows)
    {
        var filtered = new List<List<string>>();
        foreach (var row in rows)
        {
            if (row.All(static cell => string.IsNullOrWhiteSpace(cell)))
            {
                continue;
            }

            filtered.Add(row);
        }

        return filtered;
    }

    private static IReadOnlyList<string> BuildHeaders(List<List<string>> rows, int columnCount, int headerRowIndex)
    {
        var headers = new List<string>(columnCount);
        IReadOnlyList<string>? headerRow = null;

        if (headerRowIndex >= 0 && headerRowIndex < rows.Count)
        {
            headerRow = rows[headerRowIndex];
        }

        for (var column = 0; column < columnCount; column++)
        {
            string name;
            if (headerRow is not null && column < headerRow.Count)
            {
                name = headerRow[column];
            }
            else
            {
                name = $"Column {column + 1}";
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Column {column + 1}";
            }

            headers.Add(EnsureUnique(headers, name.Trim()));
        }

        return headers;
    }

    private static string EnsureUnique(List<string> existing, string candidate)
    {
        if (!existing.Any(name => string.Equals(name, candidate, StringComparison.Ordinal)))
        {
            return candidate;
        }

        var suffix = 2;
        var baseName = candidate;
        while (existing.Any(name => string.Equals(name, $"{baseName} ({suffix})", StringComparison.Ordinal)))
        {
            suffix++;
        }

        return $"{baseName} ({suffix})";
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractDataRows(List<List<string>> rows, int headerRowIndex)
    {
        var data = new List<IReadOnlyList<string>>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (i == headerRowIndex)
            {
                continue;
            }

            data.Add(rows[i]);
        }

        return data;
    }

    private sealed record AdjustedTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

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
