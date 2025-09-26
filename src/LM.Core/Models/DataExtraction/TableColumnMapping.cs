#nullable enable

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Column level metadata describing how extracted CSV columns should be interpreted.
    /// </summary>
    public sealed class TableColumnMapping
    {
        public int ColumnIndex { get; init; }
        public string Header { get; init; } = string.Empty;
        public TableColumnRole Role { get; init; } = TableColumnRole.Unknown;
        public string NormalizedHeader { get; init; } = string.Empty;
    }
}
