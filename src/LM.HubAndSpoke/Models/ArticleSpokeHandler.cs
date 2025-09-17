#nullable enable
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Abstractions;
using LM.HubSpoke.FileSystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.HubSpoke.Models;

namespace LM.HubSpoke.Spokes
{
    public sealed class ArticleSpokeHandler : ISpokeHandler
    {
        public EntryType Handles => EntryType.Publication;
        public string HookPath => "hooks/article.json";

        public async Task<object?> BuildHookAsync(
            Entry entry,
            CasResult primary,
            IEnumerable<string> attachmentRelPaths,
            Func<string, CancellationToken, Task<CasResult>> moveToCas,
            CancellationToken ct)
        {
            // Build a Publication hook
            var hook = new ArticleHook
            {
                Article = new ArticleDetails
                {
                    Title = entry.Title ?? entry.DisplayName ?? string.Empty,
                    Dates = new ArticleDates() // leave exact instants empty unless you have them
                },
                Identifier = new ArticleIdentifier
                {
                    DOI = entry.Doi,
                    PMID = entry.Pmid ?? string.Empty
                },
                Journal = new JournalInfo
                {
                    Title = entry.Source ?? string.Empty,
                    // Always create Issue; only PubDate is optional (avoids nullability warnings)
                    Issue = new JournalIssue
                    {
                        PubDate = entry.Year.HasValue
                            ? new PartialDate { Year = entry.Year.Value }
                            : null
                    }
                },
                Authors = (entry.Authors ?? new List<string>())
                          .Select(a => new Author { LastName = a }).ToList(),
                Keywords = new List<string>()
            };

            if (primary.RelPath is not null)
            {
                hook.Assets.Add(new ArticleAsset
                {
                    Title = Path.GetFileName(primary.RelPath),
                    OriginalFilename = primary.Original,
                    StoragePath = primary.RelPath,
                    Hash = primary.Sha is null ? "" : $"sha256-{primary.Sha}",
                    ContentType = primary.Mime ?? "application/octet-stream",
                    Bytes = primary.Bytes,
                    Purpose = ArticleAssetPurpose.Manuscript
                });
            }

            foreach (var rel in attachmentRelPaths)
            {
                var moved = await moveToCas(rel, ct);
                if (moved.RelPath is null) continue;

                hook.Assets.Add(new ArticleAsset
                {
                    Title = Path.GetFileName(moved.RelPath),
                    OriginalFilename = Path.GetFileName(rel),
                    StoragePath = moved.RelPath,
                    Hash = moved.Sha is null ? "" : $"sha256-{moved.Sha}",
                    ContentType = moved.Mime ?? "application/octet-stream",
                    Bytes = moved.Bytes,
                    Purpose = ArticleAssetPurpose.Supplement
                });
            }

            return hook;
        }

        public async Task<object?> LoadHookAsync(IWorkSpaceService ws, string entryId, CancellationToken ct)
        {
            var p = WorkspaceLayout.ArticleHookPath(ws, entryId);
            if (!File.Exists(p)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(p, ct);
                return JsonSerializer.Deserialize<ArticleHook>(json, JsonStd.Options);
            }
            catch { return null; }
        }

        public Entry MapToEntry(EntryHub hub, object? hookObj)
        {
            var hook = hookObj as ArticleHook;

            var e = new Entry
            {
                Id = hub.EntryId,
                Title = hook?.Article?.Title ?? hub.DisplayTitle,
                DisplayName = hub.DisplayTitle,
                AddedOnUtc = hub.CreatedUtc,
                IsInternal = hub.Origin == EntryOrigin.Internal,
                Doi = hook?.Identifier?.DOI,
                Pmid = hook?.Identifier?.PMID,
                Source = hook?.Journal?.Title,
                Tags = hub.Tags?.ToList() ?? new List<string>(),
                Authors = (hook?.Authors ?? new List<Author>())
                              .Select(a => string.Join(' ', new[] { a.ForeName, a.LastName }
                                  .Where(s => !string.IsNullOrWhiteSpace(s))))
                              .Where(s => !string.IsNullOrWhiteSpace(s))
                              .ToList(),
                Year = hook?.Journal?.Issue?.PubDate?.Year
                           ?? hook?.Article?.Dates?.Print?.Year
                           ?? hook?.Article?.Dates?.Electronic?.Year,
                Type = EntryType.Publication
            };

            var primary = hook?.Assets?.FirstOrDefault(a => a.Purpose == ArticleAssetPurpose.Manuscript)
                       ?? hook?.Assets?.FirstOrDefault();

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
            var hook = hookObj as ArticleHook;

            var authors = (hook?.Authors ?? new List<Author>())
                .Select(a => string.Join(' ', new[] { a.ForeName, a.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s))))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var hashes = (hook?.Assets ?? new List<ArticleAsset>())
                .Select(a => (a.Hash ?? "").Replace("sha256-", ""))
                .Where(h => h.Length == 64)
                .ToList();

            return new SpokeIndexContribution(
                Title: hook?.Article?.Title ?? hub.DisplayTitle,
                Abstract: hook?.Abstract?.Text,
                Authors: authors,
                Keywords: hook?.Keywords ?? new List<string>(),
                Journal: hook?.Journal?.Title,
                Doi: hook?.Identifier?.DOI,
                Pmid: hook?.Identifier?.PMID,
                Year: hook?.Journal?.Issue?.PubDate?.Year
                      ?? hook?.Article?.Dates?.Print?.Year
                      ?? hook?.Article?.Dates?.Electronic?.Year,
                AssetHashes: hashes,
                FullText: fullText
            );
        }
    }
}
