#nullable enable

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Semantic hint describing how a column should be interpreted by downstream reviewers.
    /// </summary>
    public enum TableColumnRole
    {
        Unknown = 0,
        Population = 1,
        Intervention = 2,
        Outcome = 3,
        Timepoint = 4,
        Value = 5,
        Measure = 6
    }
}
