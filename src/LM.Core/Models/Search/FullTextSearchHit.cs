namespace LM.Core.Models.Search
{
    public sealed record FullTextSearchHit(string EntryId, double Score, string? Highlight);
}
