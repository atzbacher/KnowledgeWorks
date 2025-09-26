#nullable enable
using System;
using System.Collections.Generic;

namespace LM.Core.Models.DataExtraction
{
    /// <summary>
    /// Provenance metadata recorded for a staged extraction bundle.
    /// </summary>
    public sealed class EvidenceProvenance
    {
        public string SourceSha256 { get; init; } = string.Empty;
        public string SourceFileName { get; init; } = string.Empty;
        public DateTime ExtractedAtUtc { get; init; } = DateTime.UtcNow;
        public string ExtractedBy { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> AdditionalMetadata { get; init; } = new Dictionary<string, string>();
    }
}
