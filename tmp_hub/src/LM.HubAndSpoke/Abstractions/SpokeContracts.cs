#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;   // IWorkSpaceService, IHasher, IContentExtractor
using LM.Core.Models;         // Entry, EntryType
using LM.HubSpoke.Indexing;   // SqliteSearchIndex
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Abstractions
{
    // Result of moving a file into the content-addressed store
    public readonly record struct CasResult(
        string? RelPath, string? Sha, long Bytes, string? Mime, string? Original);

    // What each spoke contributes to the search index
    public readonly record struct SpokeIndexContribution(
        string? Title,
        string? Abstract,
        IReadOnlyList<string> Authors,
        IReadOnlyList<string> Keywords,
        string? Journal,
        string? Doi,
        string? Pmid,
        int? Year,
        IReadOnlyList<string> AssetHashes,
        string? FullText
    );

    public interface ISpokeHandler
    {
        // Which LM.Core.Models.EntryType this handler is for
        EntryType Handles { get; }

        // Relative path under entries/<id>/ for the hook file
        string HookPath { get; } // e.g. "hooks/article.json"

        // Build a hook for this type from the Entry and the CAS results
        Task<object?> BuildHookAsync(
            Entry entry,
            CasResult primary,
            IEnumerable<string> attachmentRelPaths,
            Func<string, CancellationToken, Task<CasResult>> moveToCas,
            CancellationToken ct);

        // Load an existing hook from disk
        Task<object?> LoadHookAsync(IWorkSpaceService ws, string entryId, CancellationToken ct);

        // Map (hub + hook) -> Entry (what the UI uses)
        Entry MapToEntry(EntryHub hub, object? hook);

        // Build an index contribution from (hub + hook)
        SpokeIndexContribution BuildIndex(EntryHub hub, object? hook, string? extractedFullText);
    }
}
