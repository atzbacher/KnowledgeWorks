#nullable enable
using System.Collections.Generic;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Structured textual section parsed from a manuscript.
    /// </summary>
    public sealed class StructuredSection
    {
        public string Heading { get; init; } = string.Empty;
        public int Level { get; init; }
        public string Body { get; init; } = string.Empty;
        public IReadOnlyList<int> PageNumbers { get; init; } = new List<int>();
    }
}
