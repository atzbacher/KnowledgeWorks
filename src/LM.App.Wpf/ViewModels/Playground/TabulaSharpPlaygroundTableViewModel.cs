#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace LM.App.Wpf.ViewModels.Playground
{
    internal sealed class TabulaSharpPlaygroundTableViewModel
    {
        private TabulaSharpPlaygroundTableViewModel(string displayName,
                                                    int pageNumber,
                                                    int index,
                                                    DataView tableView,
                                                    int rowCount)
        {
            DisplayName = displayName;
            PageNumber = pageNumber;
            Index = index;
            TableView = tableView;
            RowCount = rowCount;
        }

        public string DisplayName { get; }

        public int PageNumber { get; }

        public int Index { get; }

        public int RowCount { get; }

        public DataView TableView { get; }

        public static TabulaSharpPlaygroundTableViewModel FromResult(TabulaSharpPlaygroundTable table)
        {
            if (table is null)
                throw new ArgumentNullException(nameof(table));

            var view = BuildView(table.Rows);
            var label = FormattableString.Invariant($"Page {table.PageNumber} · Table {table.Index}");
            return new TabulaSharpPlaygroundTableViewModel(label, table.PageNumber, table.Index, view, table.RowCount);
        }

        private static DataView BuildView(IReadOnlyList<string[]> rows)
        {
            var table = new DataTable();
            var columnCount = rows.Count == 0 ? 0 : rows.Max(r => r.Length);

            for (var i = 0; i < columnCount; i++)
            {
                table.Columns.Add(FormattableString.Invariant($"Column {i + 1}"), typeof(string));
            }

            foreach (var row in rows)
            {
                var dataRow = table.NewRow();
                for (var i = 0; i < columnCount; i++)
                {
                    dataRow[i] = i < row.Length ? row[i] : string.Empty;
                }

                table.Rows.Add(dataRow);
            }

            return table.DefaultView;
        }
    }
}
