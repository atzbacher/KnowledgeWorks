using System;
using System.Collections.Generic;
using LM.Core.Models;

namespace LM.Core.Models.Filters
{
    /// <summary>
    /// Fielded filters for Library (metadata-only for now).
    /// </summary>
    public sealed class EntryFilter
    {
        public string? TitleContains { get; set; }
        public string? AuthorContains { get; set; }
        public List<string> TagsAny { get; set; } = new();
        public EntryType[]? TypesAny { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public bool? IsInternal { get; set; } // null = either
    }
}
