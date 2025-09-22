using System;

namespace LM.Core.Models
{
    /// <summary>
    /// Event payload raised when a region export completes successfully.
    /// Surfaces to the UI and hub ingestion pipeline per the extractor design documentation.
    /// </summary>
    public sealed partial class RegionExportCompleted
    {
        /// <summary>
        /// Gets or sets the request that initiated the export.
        /// </summary>
        public RegionExportRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets the resulting export payload.
        /// </summary>
        public RegionExportResult Result { get; set; } = new();

        /// <summary>
        /// Gets or sets the completion timestamp in UTC.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    }
}
