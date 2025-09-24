using System;
using System.Collections.Generic;

namespace LM.Core.Models
{
    public enum EntryType
    {
        Publication,
        Presentation,
        WhitePaper,
        SlideDeck,
        Report,
        Other,
        LitSearch
    }

    public enum AttachmentKind
    {
        Supplement,
        Version,
        Presentation,
        ExternalNotes
    }

    /// <summary>
    /// Source-of-truth entry, serialized as entries\{id}\entry.json (camelCase).
    /// </summary>
    public sealed class Entry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        // Core metadata
        public string Title { get; set; } = string.Empty;
        public string? DisplayName { get; set; }  // human-friendly label shown in UI
        public string? ShortTitle { get; set; }   // e.g., "Smith J., NEJM, 2024"
        public EntryType Type { get; set; } = EntryType.Publication;
        public int? Year { get; set; }
        public string? Source { get; set; }       // journal/venue/source (if known)

        // People & provenance
        public List<string> Authors { get; set; } = new();
        public string? AddedBy { get; set; }
        public DateTime AddedOnUtc { get; set; } = DateTime.UtcNow;

        // IDs & links
        public string? InternalId { get; set; }
        public string? Doi { get; set; }
        public string? Pmid { get; set; }
        public string? Nct { get; set; }
        public List<string> Links { get; set; } = new();

        // Security
        public bool IsInternal { get; set; }

        // Tags (manual + inherited later)
        public List<string> Tags { get; set; } = new();

        // Main file (relative to workspace)
        public string MainFilePath { get; set; } = string.Empty;
        public string? MainFileHashSha256 { get; set; }
        public string? OriginalFileName { get; set; } // for traceability
        public int Version { get; set; } = 1;

        // Notes
        public string? Notes { get; set; }

        // Optional free-form notes captured from the UI (unstructured)
        public string? UserNotes { get; set; }

        // Attachments & relations
        public List<Attachment> Attachments { get; set; } = new();
        public List<Relation> Relations { get; set; } = new();
    }

    public sealed class Attachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RelativePath { get; set; } = string.Empty; // relative to workspace
        public List<string> Tags { get; set; } = new();
        public string? Notes { get; set; }
        public string Title { get; set; } = string.Empty;
        public AttachmentKind Kind { get; set; } = AttachmentKind.Supplement;
        public string AddedBy { get; set; } = string.Empty;
        public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Link between entries (e.g., variant_of, derived_from, summarizes)
    /// </summary>
    public sealed class Relation
    {
        public string Type { get; set; } = "variant_of";
        public string TargetEntryId { get; set; } = string.Empty;
    }
}
