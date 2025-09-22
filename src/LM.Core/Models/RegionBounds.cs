using System;

namespace LM.Core.Models
{
    /// <summary>
    /// Describes the rectangular coordinates of an extracted region in pixel space.
    /// Coordinates follow the JSON schema documented in
    /// <c>docs/visual-extractor/data-storage.md</c> (x, y, width, height).
    /// </summary>
    public sealed partial class RegionBounds
    {
        /// <summary>
        /// Gets or sets the X coordinate (in pixels) relative to the source page or slide.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate (in pixels) relative to the source page or slide.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the width of the region in pixels.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the region in pixels.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Gets a value indicating whether the region is empty (width or height less than or equal to zero).
        /// </summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>
        /// Creates a shallow copy of the current bounds instance.
        /// </summary>
        public RegionBounds Clone() => (RegionBounds)MemberwiseClone();
    }
}
