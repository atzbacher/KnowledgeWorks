using System;

namespace LM.Core.Models
{
    /// <summary>
    /// Represents a lightweight bitmap preview for quick redraws in the extraction UI.
    /// Aligns with <c>IRegionPreviewService</c> responsibilities documented in the extractor specs.
    /// </summary>
    public sealed partial class RegionPreview
    {
        /// <summary>
        /// Gets or sets the stable cache key used by preview caches.
        /// </summary>
        public string CacheKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the on-disk path to the preview bitmap.
        /// </summary>
        public string PreviewPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the pixel width of the preview image.
        /// </summary>
        public int PixelWidth { get; set; }

        /// <summary>
        /// Gets or sets the pixel height of the preview image.
        /// </summary>
        public int PixelHeight { get; set; }

        /// <summary>
        /// Gets or sets the horizontal DPI metadata associated with the preview image.
        /// </summary>
        public double DpiX { get; set; } = 96d;

        /// <summary>
        /// Gets or sets the vertical DPI metadata associated with the preview image.
        /// </summary>
        public double DpiY { get; set; } = 96d;

        /// <summary>
        /// Gets or sets the timestamp when the preview was generated.
        /// </summary>
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets a value indicating whether the preview originated from cache.
        /// </summary>
        public bool FromCache { get; set; }
    }
}
