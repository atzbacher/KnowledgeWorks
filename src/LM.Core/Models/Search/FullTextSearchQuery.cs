using System;
using LM.Core.Models.Filters;

namespace LM.Core.Models.Search
{
    [Flags]
    public enum FullTextSearchField
    {
        None = 0,
        Title = 1 << 0,
        Abstract = 1 << 1,
        Content = 1 << 2
    }

    public sealed class FullTextSearchQuery
    {
        public string? Text { get; set; }

        public FullTextSearchField Fields { get; set; }
            = FullTextSearchField.Title | FullTextSearchField.Abstract | FullTextSearchField.Content;

        public int? YearFrom { get; set; }

        public int? YearTo { get; set; }

        public bool? IsInternal { get; set; }

        public EntryType[]? TypesAny { get; set; }

        public int Limit { get; set; } = 100;
    }
}
