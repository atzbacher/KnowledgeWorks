#nullable enable
using System;

namespace LM.Core.Models
{
    /// <summary>Provider-agnostic search row used in the Search tab UI.</summary>
    public sealed class SearchHit
    {
        public SearchDatabase Source { get; init; }
        public string ExternalId { get; init; } = "";   // PMID or NCT
        public string? Doi { get; init; }
        public string Title { get; init; } = "";
        public string Authors { get; init; } = "";
        public string? JournalOrSource { get; init; }
        public int? Year { get; init; }
        public string? Url { get; init; }

        public bool AlreadyInDb { get; set; }    // computed in VM
        public bool Selected { get; set; } = true;
    }
}
