#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LM.App.Wpf.ViewModels.Library;

internal static class TesseractTableBuilder
{
    internal static TesseractTableResult Build(string? tsv)
    {
        if (string.IsNullOrWhiteSpace(tsv))
        {
            return new TesseractTableResult(new List<IReadOnlyList<string>>(), 0);
        }

        var lines = tsv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new TesseractTableResult(new List<IReadOnlyList<string>>(), 0);
        }

        var words = new List<TesseractWord>();
        for (var i = 1; i < lines.Length; i++)
        {
            var columns = lines[i].Split('\t');
            if (columns.Length < 12)
            {
                continue;
            }

            if (!int.TryParse(columns[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var line))
            {
                continue;
            }

            if (!int.TryParse(columns[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left))
            {
                continue;
            }

            if (!int.TryParse(columns[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
            {
                continue;
            }

            var text = columns[11].Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            words.Add(new TesseractWord(line, left, width, text));
        }

        if (words.Count == 0)
        {
            return new TesseractTableResult(new List<IReadOnlyList<string>>(), 0);
        }

        var averageWidth = words.Average(static w => w.Width);
        var gapThreshold = Math.Max(12, averageWidth * 1.25);
        var rows = new List<IReadOnlyList<string>>();

        foreach (var group in words.GroupBy(static w => w.Line).OrderBy(static g => g.Key))
        {
            var sorted = group.OrderBy(static w => w.Left).ToList();
            var columns = new List<string>();
            var buffer = new StringBuilder();
            int? previousRight = null;

            foreach (var word in sorted)
            {
                if (previousRight.HasValue && word.Left - previousRight.Value > gapThreshold)
                {
                    columns.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }

                if (buffer.Length > 0)
                {
                    buffer.Append(' ');
                }

                buffer.Append(word.Text);
                previousRight = word.Left + word.Width;
            }

            if (buffer.Length > 0)
            {
                columns.Add(buffer.ToString().Trim());
            }

            if (columns.Count == 0)
            {
                columns.Add(string.Join(' ', sorted.Select(static w => w.Text))); 
            }

            rows.Add(columns);
        }

        return Normalize(rows);
    }

    internal static TesseractTableResult FromPlainText(IEnumerable<string> lines)
    {
        var materialized = lines?.Select(static line => line?.Trim() ?? string.Empty)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToList() ?? new List<string>();

        if (materialized.Count == 0)
        {
            return new TesseractTableResult(new List<IReadOnlyList<string>>(), 0);
        }

        var rows = new List<IReadOnlyList<string>>(materialized.Count);
        foreach (var line in materialized)
        {
            var segments = line.Split('\t');
            if (segments.Length == 0)
            {
                rows.Add(new[] { line });
                continue;
            }

            rows.Add(segments.Select(static segment => segment.Trim()).ToList());
        }

        return Normalize(rows);
    }

    private static TesseractTableResult Normalize(IReadOnlyList<IReadOnlyList<string>> source)
    {
        if (source.Count == 0)
        {
            return new TesseractTableResult(new List<IReadOnlyList<string>>(), 0);
        }

        var maxColumns = source.Max(static row => row?.Count ?? 0);
        if (maxColumns == 0)
        {
            maxColumns = 1;
        }

        var rows = new List<IReadOnlyList<string>>(source.Count);
        foreach (var row in source)
        {
            if (row is null)
            {
                rows.Add(Enumerable.Repeat(string.Empty, maxColumns).ToArray());
                continue;
            }

            if (row.Count == maxColumns)
            {
                rows.Add(row.ToArray());
                continue;
            }

            var values = new List<string>(row);
            while (values.Count < maxColumns)
            {
                values.Add(string.Empty);
            }

            rows.Add(values);
        }

        return new TesseractTableResult(rows, maxColumns);
    }

    private sealed record TesseractWord(int Line, int Left, int Width, string Text);
}

internal sealed record TesseractTableResult(IReadOnlyList<IReadOnlyList<string>> Rows, int ColumnCount);
