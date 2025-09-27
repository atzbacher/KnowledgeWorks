#nullable enable
using System;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Absolute coordinates for a table within a PDF page measured in points.
    /// </summary>
    public sealed class TablePageLocation
    {
        public int PageNumber { get; init; }
        public double Left { get; init; }
        public double Top { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double PageWidth { get; init; }
        public double PageHeight { get; init; }

    }
}
