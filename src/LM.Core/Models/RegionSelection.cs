using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Captures a user's visual selection, including geometry, annotations, and tagging metadata.
    /// This model originates from the WPF extraction canvas as described in
    /// <c>docs/visual-extractor/architecture.md</c>.
    /// </summary>
    public sealed partial class RegionSelection
    {
        /// <summary>
        /// Gets or sets the bounding box for the selection in pixel coordinates.
        /// </summary>
        public RegionBounds Bounds { get; set; } = new();

        /// <summary>
        /// Gets or sets the page or slide number (1-based) associated with the selection.
        /// </summary>
        public int? PageNumber { get; set; }

        /// <summary>
        /// Gets or sets the zoom factor used when the region was captured (1.0 represents 100%).
        /// </summary>
        public double ZoomLevel { get; set; } = 1d;

        /// <summary>
        /// Gets or sets the rotation applied to the source page or slide, expressed in degrees.
        /// </summary>
        public double RotationDegrees { get; set; }

        /// <summary>
        /// Gets or sets the free-form annotation entered at capture time (displayed in the metadata sidebar).
        /// </summary>
        public string? Annotation { get; set; }

        /// <summary>
        /// Gets or sets additional operator notes stored with the selection.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Gets the list of tags applied to the selection.
        /// </summary>
        public List<string> Tags { get; } = new();
    }
}
