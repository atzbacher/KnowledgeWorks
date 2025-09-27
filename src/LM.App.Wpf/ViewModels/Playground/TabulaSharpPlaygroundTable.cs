#nullable enable
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Playground
{
    internal sealed class TabulaSharpPlaygroundTable
    {
        public TabulaSharpPlaygroundTable(int pageNumber, int index, IReadOnlyList<string[]> rows)
        {
            PageNumber = pageNumber;
            Index = index;
            Rows = rows;
        }

        public int PageNumber { get; }

        public int Index { get; }

        public IReadOnlyList<string[]> Rows { get; }

        public int RowCount => Rows.Count;
    }
}
