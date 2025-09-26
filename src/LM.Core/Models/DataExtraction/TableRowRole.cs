#nullable enable

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Semantic hint describing what a row represents in an extracted table.
    /// </summary>
    public enum TableRowRole
    {
        Unknown = 0,
        Header = 1,
        Baseline = 2,
        Outcome = 3,
        Footnote = 4
    }
}
