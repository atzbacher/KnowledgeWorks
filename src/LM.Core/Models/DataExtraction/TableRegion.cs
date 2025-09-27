#nullable enable
using System;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Normalized rectangle describing where a table resides on a PDF page.
    /// Coordinates are normalized to the page width/height using a top-left origin.
    /// </summary>
    public sealed class TableRegion
    {
        public int PageNumber { get; init; }

        /// <summary>Normalized X coordinate (0..1) relative to the left edge of the page.</summary>
        public double X { get; init; }

        /// <summary>Normalized Y coordinate (0..1) relative to the top edge of the page.</summary>
        public double Y { get; init; }

        /// <summary>Normalized width (0..1) of the table region.</summary>
        public double Width { get; init; }

        /// <summary>Normalized height (0..1) of the table region.</summary>
        public double Height { get; init; }

        public string? Label { get; init; }

    }
}
