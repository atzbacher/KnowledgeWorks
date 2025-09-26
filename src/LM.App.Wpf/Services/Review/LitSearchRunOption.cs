#nullable enable
using System;
using System.Collections.Generic;

namespace LM.App.Wpf.Services.Review
{
    internal sealed record LitSearchRunOption(
        string EntryId,
        string Label,
        string Query,
        string HookAbsolutePath,
        string HookRelativePath,
        IReadOnlyList<LitSearchRunOptionRun> Runs);

    internal sealed record LitSearchRunOptionRun(
        string RunId,
        DateTime RunUtc,
        int TotalHits,
        string? ExecutedBy,
        bool IsFavorite);
}
