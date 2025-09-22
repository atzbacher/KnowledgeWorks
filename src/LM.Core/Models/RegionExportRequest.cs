using System;
using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Represents an export command issued by the visual extraction workflow.
    /// Contains the selection, source provenance, and exporter preferences described in
    /// <c>docs/visual-extractor/service-contracts.md</c>.
    /// </summary>
    public sealed partial class RegionExportRequest
    {
        /// <summary>
        /// Gets or sets the entry hub identifier that owns the source document.
        /// </summary>
        public string EntryHubId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relative path to the source file inside the workspace.
        /// </summary>
        public string SourceRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SHA-256 hash of the source file used to derive region hashes.
        /// </summary>
        public string? SourceSha256 { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the source file (used to dispatch to exporters).
        /// </summary>
        public string? SourceMimeType { get; set; }

        /// <summary>
        /// Gets or sets the selection captured by the UI.
        /// </summary>
        public RegionSelection Selection { get; set; } = new();

        /// <summary>
        /// Gets or sets the identifier of the exporter that should handle this request.
        /// </summary>
        public string ExporterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether cached outputs can satisfy the request.
        /// </summary>
        public bool AllowCachedResult { get; set; } = true;

        /// <summary>
        /// Gets or sets when the export was queued (UTC).
        /// </summary>
        public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the user identifier that initiated the export, if tracked.
        /// </summary>
        public string? RequestedBy { get; set; }

        /// <summary>
        /// Gets exporter-specific options (e.g., DPI overrides, color profiles).
        /// </summary>
        public Dictionary<string, string> ExportOptions { get; } = new();

        /// <summary>
        /// Gets or sets the Office destination hint (e.g., slide id) when routing to the add-in bridge.
        /// </summary>
        public string? OfficeDestination { get; set; }
    }
}
