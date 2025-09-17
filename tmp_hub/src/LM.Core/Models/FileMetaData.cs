using System.Collections.Generic;

namespace LM.Core.Models
{
    /// <summary>
    /// Lightweight metadata discovered during import.
    /// </summary>
    public sealed class FileMetadata
    {
        public string? Title { get; set; }
        public List<string> Authors { get; set; } = new();

        /// <summary>Publication year if discoverable.</summary>
        public int? Year { get; set; }

        /// <summary>Journal / conference / source if discoverable.</summary>
        public string? Source { get; set; }

        /// <summary>Canonical identifiers when found in file or properties.</summary>
        public string? Doi { get; set; }
        public string? Pmid { get; set; }

        /// <summary>Sanitized keywords/tags extracted from the file's built-in properties.</summary>
        public List<string> Tags { get; set; } = new();
    }
}
