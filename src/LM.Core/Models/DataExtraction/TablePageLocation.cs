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

        public static TablePageLocation Create(int pageNumber,
                                               double left,
                                               double top,
                                               double width,
                                               double height,
                                               double pageWidth,
                                               double pageHeight)
        {
            if (pageWidth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pageWidth));
            if (pageHeight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pageHeight));

            return new TablePageLocation
            {
                PageNumber = Math.Max(1, pageNumber),
                Left = Math.Max(0d, left),
                Top = Math.Max(0d, top),
                Width = Math.Max(0d, width),
                Height = Math.Max(0d, height),
                PageWidth = pageWidth,
                PageHeight = pageHeight
            };
        }
    }
}
