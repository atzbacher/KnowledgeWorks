#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Spokes
{
    public sealed class DocumentSpokeHandler : ISpokeHandler
    {
        public EntryType Handles => EntryType.Report;     // use your enum value for “generic doc”; adjust if you prefer Other
        public string HookPath => "hooks/document.json";

        public async Task<object?> BuildHookAsync(
            Entry entry,
            CasResult primary,
            IEnumerable<string> attachmentRelPaths,
            Func<string, CancellationToken, Task<CasResult>> moveToCas,
            CancellationToken ct)
        {
            var assets = new List<AssetRef>();
            if (primary.RelPath is not null)
                assets.Add(new AssetRef
                {
                    Role = "primary",
                    Hash = primary.Sha is null ? "" : $"sha256-{primary.Sha}",
                    StoragePath = primary.RelPath,
                    ContentType = primary.Mime ?? "application/octet-stream",
                    Bytes = primary.Bytes,
                    OriginalFilename = primary.Original
                });

            foreach (var rel in attachmentRelPaths)
            {
                var moved = await moveToCas(rel, ct);
                if (moved.RelPath is null) continue;
                assets.Add(new AssetRef
                {
                    Role = "supplement",
                    Hash = moved.Sha is null ? "" : $"sha256-{moved.Sha}",
                    StoragePath = moved.RelPath,
                    ContentType = moved.Mime ?? "application/octet-stream",
                    Bytes = moved.Bytes,
                    OriginalFilename = Path.GetFileName(rel)
                });
            }

            return new DocumentHook { Title = entry.Title ?? entry.DisplayName ?? string.Empty, Assets = assets };
        }

        public async Task<object?> LoadHookAsync(IWorkSpaceService ws, string entryId, CancellationToken ct)
        {
            var p = WorkspaceLayout.DocumentHookPath(ws, entryId);
            if (!File.Exists(p)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(p, ct);
                return JsonSerializer.Deserialize<DocumentHook>(json, JsonStd.Options);
            }
            catch { return null; }
        }

        public Entry MapToEntry(EntryHub hub, object? hookObj)
        {
            var hook = hookObj as DocumentHook;
            var e = new Entry
            {
                Id = hub.EntryId,
                Title = hook?.Title ?? hub.DisplayTitle,
                DisplayName = hub.DisplayTitle,
                AddedOnUtc = hub.CreatedUtc,
                IsInternal = hub.Origin == EntryOrigin.Internal,
                Source = null,
                Tags = hub.Tags?.ToList() ?? new List<string>(),
                Authors = new List<string>(),
                Year = hub.Tags?.FirstOrDefault() == null ? null : null, // keep neutral; index uses hub.Year if given
                Type = EntryType.Report
            };

            var primary = hook?.Assets?.FirstOrDefault(a => a.Role == "primary") ?? hook?.Assets?.FirstOrDefault();
            if (primary is not null)
            {
                e.MainFilePath = primary.StoragePath.Replace('\\', '/');
                if (primary.Hash?.StartsWith("sha256-") == true)
                    e.MainFileHashSha256 = primary.Hash.Substring("sha256-".Length);
                e.OriginalFileName = primary.OriginalFilename;
            }
            return e;
        }

        public SpokeIndexContribution BuildIndex(EntryHub hub, object? hookObj, string? fullText)
        {
            var hook = hookObj as DocumentHook;
            var hashes = (hook?.Assets ?? new List<AssetRef>())
                .Select(a => (a.Hash ?? "").Replace("sha256-", ""))
                .Where(h => h.Length == 64)
                .ToList();

            return new SpokeIndexContribution(
                Title: hook?.Title ?? hub.DisplayTitle,
                Abstract: null,
                Authors: new List<string>(),
                Keywords: new List<string>(),
                Journal: null,
                Doi: null,
                Pmid: null,
                Year: null,     // use hub.Year if you later add it to hub
                AssetHashes: hashes,
                FullText: fullText
            );
        }
    }
}
