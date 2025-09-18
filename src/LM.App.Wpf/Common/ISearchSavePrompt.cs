#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Common
{
    public interface ISearchSavePrompt
    {
        Task<SearchSavePromptResult?> RequestAsync(SearchSavePromptContext context);
    }

    public sealed record SearchSavePromptContext(
        string Query,
        SearchDatabase Database,
        DateTime? From,
        DateTime? To,
        string DefaultName,
        string DefaultNotes,
        IReadOnlyList<string> DefaultTags);

    public sealed record SearchSavePromptResult(string Name, string Notes, string Tags);
}
