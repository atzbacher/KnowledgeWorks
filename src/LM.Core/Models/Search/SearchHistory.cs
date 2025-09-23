using System;
using System.Collections.Generic;
using LM.Core.Models;

namespace LM.Core.Models.Search
{
    /// <summary>Collection of executed search history entries.</summary>
    public sealed record class SearchHistoryDocument
    {
        public IReadOnlyList<SearchHistoryEntry> Entries { get; init; }
            = Array.Empty<SearchHistoryEntry>();
    }

    /// <summary>Represents a single search execution.</summary>
    public sealed record class SearchHistoryEntry
    {
        public string Query { get; init; } = string.Empty;
        public SearchDatabase Database { get; init; } = SearchDatabase.PubMed;
        public DateTime? From { get; init; }
            = null;
        public DateTime? To { get; init; }
            = null;
        public DateTimeOffset ExecutedUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
