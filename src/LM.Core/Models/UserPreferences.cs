using System;

namespace LM.Core.Models
{
    /// <summary>Persisted application preferences.</summary>
    public sealed record class UserPreferences
    {
        public SearchPreferences Search { get; init; } = new();
    }

    /// <summary>Preferences specific to the search experience.</summary>
    public sealed record class SearchPreferences
    {
        public SearchDatabase LastSelectedDatabase { get; init; } = SearchDatabase.PubMed;
        public DateTimeOffset LastUpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
