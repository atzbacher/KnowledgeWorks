using System;
using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Represents the outcome of a region export operation, including generated assets and metadata updates.
    /// Mirrors the data returned by <see cref="Abstractions.IRegionExporter"/> implementations.
    /// </summary>
    public sealed partial class RegionExportResult
    {
        /// <summary>
        /// Gets or sets the exporter identifier that produced this result.
        /// </summary>
        public string ExporterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the descriptor persisted for the exported region.
        /// </summary>
        public RegionDescriptor Descriptor { get; set; } = new();

        /// <summary>
        /// Gets or sets the path to the exported raster image asset.
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the exported OCR text asset, if produced.
        /// </summary>
        public string? OcrTextPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the Office-ready package (PPMX) if generated.
        /// </summary>
        public string? OfficePackagePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result was served from cached assets.
        /// </summary>
        public bool WasCached { get; set; }

        /// <summary>
        /// Gets or sets the duration of the export operation.
        /// </summary>
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the completion timestamp in UTC.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets auxiliary exporter outputs (e.g., color palette JSON, vector snapshots).
        /// </summary>
        public Dictionary<string, string> AdditionalOutputs { get; } = new();
    }
}
