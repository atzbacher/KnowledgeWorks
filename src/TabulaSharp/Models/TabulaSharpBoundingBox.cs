using System;

namespace TabulaSharp.Models
{
    /// <summary>
    /// Represents a rectangular bounding box in PDF coordinates.
    /// </summary>
    public readonly struct TabulaSharpBoundingBox
    {
        public TabulaSharpBoundingBox(double left, double bottom, double right, double top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public double Left { get; }

        public double Bottom { get; }

        public double Right { get; }

        public double Top { get; }

        public double Width => Math.Max(0d, Right - Left);

        public double Height => Math.Max(0d, Top - Bottom);

        public bool IsValid => Width > 0d && Height > 0d;

        public TabulaSharpBoundingBox Expand(double amount)
        {
            return new TabulaSharpBoundingBox(Left - amount, Bottom - amount, Right + amount, Top + amount);
        }
    }
}
