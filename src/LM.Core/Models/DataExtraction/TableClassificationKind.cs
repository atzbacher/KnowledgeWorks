#nullable enable

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// High level characterization of a detected evidence table.
    /// </summary>
    public enum TableClassificationKind
    {
        Unknown = 0,
        Baseline = 1,
        Outcome = 2,
        Mixed = 3
    }
}
