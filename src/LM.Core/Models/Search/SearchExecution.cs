using System;
using System.Collections.Generic;
using LM.Core.Models;

namespace LM.Core.Models.Search
{
    /// <summary>
    /// Parameters describing a search execution request.
    /// </summary>
    public sealed record class SearchExecutionRequest
    {
        public string Query { get; init; } = string.Empty;
        public SearchDatabase Database { get; init; }
            = SearchDatabase.PubMed;
        public DateTime? From { get; init; }
            = null;
        public DateTime? To { get; init; }
            = null;
    }

    /// <summary>
    /// Result returned after executing a search.
    /// </summary>
    public sealed record class SearchExecutionResult
    {
        public required SearchExecutionRequest Request { get; init; }
            = new();
        public required DateTimeOffset ExecutedUtc { get; init; }
            = DateTimeOffset.UtcNow;
        public IReadOnlyList<SearchHit> Hits { get; init; }
            = Array.Empty<SearchHit>();
    }
}
