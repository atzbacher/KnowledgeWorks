#nullable enable

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Row level metadata describing role assignments for extracted tables.
    /// </summary>
    public sealed class TableRowMapping
    {
        public int RowIndex { get; init; }
        public string Label { get; init; } = string.Empty;
        public TableRowRole Role { get; init; } = TableRowRole.Unknown;
    }
}
