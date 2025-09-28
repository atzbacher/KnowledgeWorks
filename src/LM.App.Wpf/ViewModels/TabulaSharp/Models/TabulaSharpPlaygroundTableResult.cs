#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TabulaSharp.Models;

namespace LM.App.Wpf.ViewModels.TabulaSharp.Models
{
    internal sealed class TabulaSharpPlaygroundTableResult
    {
        public TabulaSharpPlaygroundTableResult(int pageNumber,
                                                int tableIndex,
                                                TabulaSharpBoundingBox bounds,
                                                IReadOnlyList<string[]> rows)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            PageNumber = pageNumber;
            TableIndex = tableIndex;
            Bounds = bounds;
            RowCount = rows.Count;
            ColumnCount = rows.Count == 0 ? 0 : rows.Max(r => r?.Length ?? 0);
            FriendlyName = FormattableString.Invariant($"Page {PageNumber} · Table {TableIndex}");
            BoundsDisplay = FormattableString.Invariant($"x={bounds.Left:0.##}, y={bounds.Bottom:0.##}, w={bounds.Width:0.##}, h={bounds.Height:0.##}");
            Preview = BuildPreview(rows);
        }

        public int PageNumber { get; }

        public int TableIndex { get; }

        public TabulaSharpBoundingBox Bounds { get; }

        public IReadOnlyList<string[]> Rows { get; }

        public int RowCount { get; }

        public int ColumnCount { get; }

        public string FriendlyName { get; }

        public string BoundsDisplay { get; }

        public string Preview { get; }

        private static string BuildPreview(IReadOnlyList<string[]> rows)
        {
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                if (row is null)
                {
                    continue;
                }

                var cells = row.Select(cell => cell?.Replace("\r", string.Empty) ?? string.Empty);
                builder.AppendLine(string.Join(" | ", cells));
            }

            return builder.ToString().TrimEnd();
        }
    }
}
