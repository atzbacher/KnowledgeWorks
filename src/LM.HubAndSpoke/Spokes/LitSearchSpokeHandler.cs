#nullable enable                  // EntryHub
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LM.HubSpoke.Spokes
{
    public sealed class LitSearchSpokeHandler : ISpokeHandler
    {
        private readonly IWorkSpaceService _ws;

        public LitSearchSpokeHandler(IWorkSpaceService workspace)
        {
            _ws = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public EntryType Handles => EntryType.Other;
        public string HookPath => "hooks/litsearch.json";

        public async Task<object?> BuildHookAsync(
            Entry entry,
            CasResult primary,
            System.Collections.Generic.IEnumerable<string> attachmentRelPaths,
            Func<string, CancellationToken, Task<CasResult>> moveToCas,
            CancellationToken ct)
        {
            // primary.RelPath points to the litsearch json we created in the Search tab
            if (string.IsNullOrWhiteSpace(primary.RelPath))
                return new LitSearchHook { Title = entry.Title ?? entry.DisplayName ?? "Search" };

            var wsJson = primary.RelPath!;
            var abs = Path.IsPathRooted(wsJson) ? wsJson : _ws.GetAbsolutePath(wsJson);

            // Load and return the LitSearchHook object so the store can persist it at HookPath
            try
            {
                var json = await File.ReadAllTextAsync(abs, ct);
                return JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
            }
            catch
            {
                return new LitSearchHook { Title = entry.Title ?? entry.DisplayName ?? "Search" };
            }
        }

        public SpokeIndexContribution BuildIndex(EntryHub hub, object? hookObj, string? extractedFullText)
        {
            var hook = hookObj as LitSearchHook;
            var abstractText = string.Join(Environment.NewLine,
                new[] { hook?.Query, hook?.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var keywords = hook?.Keywords ?? Array.Empty<string>();
            return new SpokeIndexContribution(
                Title: hook?.Title ?? hub.DisplayTitle,
                Abstract: string.IsNullOrWhiteSpace(abstractText) ? null : abstractText,
                Authors: Array.Empty<string>(),
                Keywords: keywords,
                Journal: null,
                Doi: null,
                Pmid: null,
                Year: hook?.From?.Year ?? hook?.To?.Year,
                AssetHashes: Array.Empty<string>(),
                FullText: extractedFullText
            );
        }

        public Entry MapToEntry(EntryHub hub, object? hookObj)
        {
            var hook = hookObj as LitSearchHook;
            return new Entry
            {
                Id = hub.EntryId,
                Type = EntryType.Other,
                Title = hook?.Title ?? hub.DisplayTitle,
                DisplayName = hook?.Title ?? hub.DisplayTitle,
                Source = "LitSearch",
                AddedOnUtc = hook?.CreatedUtc ?? hub.CreatedUtc,
                AddedBy = hook?.CreatedBy,
                Notes = hook?.Notes,
                Tags = hub.Tags?.ToList() ?? new List<string>()
            };
        }

        public Task<object?> LoadHookAsync(IWorkSpaceService ws, string entryId, CancellationToken ct)
        {
            // store already loads hooks from HookPath; no custom load logic needed
            return Task.FromResult<object?>(null);
        }
    }
}
