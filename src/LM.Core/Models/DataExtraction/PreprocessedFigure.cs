#nullable enable
using System;
using System.Collections.Generic;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Metadata describing a detected figure and its staged thumbnail.
    /// </summary>
    public sealed class PreprocessedFigure
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Caption { get; init; } = string.Empty;
        public IReadOnlyList<int> PageNumbers { get; init; } = new List<int>();
        public string ThumbnailRelativePath { get; init; } = string.Empty;
        public string ProvenanceHash { get; init; } = string.Empty;
    }
}
