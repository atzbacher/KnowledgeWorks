using System;
using System.Collections.Generic;
using LM.Core.Models;

namespace LM.Core.Models.Filters
{
    /// <summary>
    /// Fielded filters for Library (metadata-only for now).
    /// </summary>
    public enum TagMatchMode
    {
        Any,
        All,
        Not
    }

    public sealed class EntryFilter
    {
        public string? TitleContains { get; set; }
        public string? AuthorContains { get; set; }
        public List<string> Tags { get; set; } = new();
        public TagMatchMode TagMatchMode { get; set; } = TagMatchMode.Any;
        public EntryType[]? TypesAny { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public bool? IsInternal { get; set; } // null = either
        public string? SourceContains { get; set; }
        public string? InternalIdContains { get; set; }
        public string? DoiContains { get; set; }
        public string? PmidContains { get; set; }
        public string? NctContains { get; set; }
        public string? AddedByContains { get; set; }
        public DateTime? AddedOnFromUtc { get; set; }
        public DateTime? AddedOnToUtc { get; set; }
    }
}
