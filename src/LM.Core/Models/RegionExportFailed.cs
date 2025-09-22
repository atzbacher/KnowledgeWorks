using System;

namespace LM.Core.Models
{
    /// <summary>
    /// Event payload raised when a region export fails.
    /// Contains sanitized error information surfaced to UI and diagnostics logs.
    /// </summary>
    public sealed partial class RegionExportFailed
    {
        /// <summary>
        /// Gets or sets the original export request.
        /// </summary>
        public RegionExportRequest Request { get; set; } = new();

        /// <summary>
        /// Gets or sets the identifier of the exporter that reported the failure.
        /// </summary>
        public string ExporterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the failure timestamp in UTC.
        /// </summary>
        public DateTime FailedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the sanitized error message suitable for user display.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional machine-readable error code for diagnostics.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the export can be retried automatically.
        /// </summary>
        public bool IsRetryable { get; set; }
    }
}
