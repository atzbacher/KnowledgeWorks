using System;
using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Canonical JSON-on-disk representation for a library entry.
    /// One file per entry in the workspace.
    /// </summary>
    public sealed class EntryDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

        // Bibliographic / metadata
        public string? Title { get; set; }
        public List<string> Authors { get; set; } = new();
        public int? Year { get; set; }
        public string? Source { get; set; }     // journal / conference
        public string? Doi { get; set; }
        public string? Pmid { get; set; }

        /// <summary>
        /// Tags from the document itself (keywords) and/or user vocabulary.
        /// </summary>
        public List<string> Tags { get; set; } = new();

        // Flags
        public bool Internal { get; set; }

        // Files attached to this entry
        public List<EntryFile> Files { get; set; } = new();
    }

    public sealed class EntryFile
    {
        public string Hash { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public string StoredFileName { get; set; } = "";
        public string RelativePath { get; set; } = ""; // relative to workspace
        public long SizeBytes { get; set; }
        public string MimeType { get; set; } = "";
    }
}
