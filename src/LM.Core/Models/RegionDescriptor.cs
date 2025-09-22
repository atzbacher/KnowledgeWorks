using System;
using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Represents a persisted region descriptor stored under <c>%WORKSPACE%/extraction</c> and indexed in SQLite.
    /// Aligns with the schema documented in <c>docs/visual-extractor/data-storage.md</c>.
    /// </summary>
    public sealed partial class RegionDescriptor
    {
        /// <summary>
        /// Gets or sets the deterministic SHA-256 hash that uniquely identifies the exported region.
        /// </summary>
        public string RegionHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the entry hub that owns the source file.
        /// </summary>
        public string EntryHubId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relative path of the source file inside the workspace.
        /// </summary>
        public string SourceRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SHA-256 hash of the source file that produced the region (optional).
        /// </summary>
        public string? SourceSha256 { get; set; }

        /// <summary>
        /// Gets or sets the page or slide number (1-based) for the source document, if applicable.
        /// </summary>
        public int? PageNumber { get; set; }

        /// <summary>
        /// Gets or sets the rectangular bounds captured at export time.
        /// </summary>
        public RegionBounds Bounds { get; set; } = new();

        /// <summary>
        /// Gets or sets the OCR text extracted from the region.
        /// </summary>
        public string? OcrText { get; set; }

        /// <summary>
        /// Gets the list of tags applied to the region descriptor.
        /// </summary>
        public List<string> Tags { get; } = new();

        /// <summary>
        /// Gets or sets user-authored notes persisted alongside the descriptor.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Gets or sets the short annotation text displayed in UI previews.
        /// </summary>
        public string? Annotation { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the descriptor was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp for the last update to the descriptor, if any.
        /// </summary>
        public DateTime? UpdatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the most recent export status for the descriptor.
        /// </summary>
        public RegionExportStatus LastExportStatus { get; set; } = RegionExportStatus.Pending;

        /// <summary>
        /// Gets or sets the path to an Office-ready package (PPMX) generated for this region.
        /// </summary>
        public string? OfficePackagePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the exported raster image (PNG/JPEG).
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the exported OCR text file.
        /// </summary>
        public string? OcrTextPath { get; set; }

        /// <summary>
        /// Gets or sets the exporter identifier that produced the latest descriptor update.
        /// </summary>
        public string? ExporterId { get; set; }

        /// <summary>
        /// Gets metadata specific to exporters or post-processors, serialized into JSON when persisted.
        /// </summary>
        public Dictionary<string, string> ExtraMetadata { get; } = new();
    }
}
