#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LM.Core.Models;
using LM.HubSpoke.Models;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library
{
    public sealed partial class LibraryResultsViewModel
    {
        private async Task<HookM.ArticleHook?> TryMergeArticleHookAsync(Entry entry,
                                                                       IReadOnlyList<Attachment> added,
                                                                       string entryId)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            if (added is null || added.Count == 0)
                return null;
            if (string.IsNullOrWhiteSpace(entryId))
                return null;

            var existingHook = await LoadArticleHookAsync(entryId).ConfigureAwait(false);
            if (existingHook is null)
                return null;

            existingHook.Assets ??= new List<HookM.ArticleAsset>();
            var existingPaths = new HashSet<string>(
                existingHook.Assets.Select(a => NormalizeStoragePath(a.StoragePath)),
                StringComparer.OrdinalIgnoreCase);

            var addedAny = false;

            foreach (var attachment in added)
            {
                if (attachment is null)
                    continue;

                var storagePath = NormalizeStoragePath(attachment.RelativePath);
                if (string.IsNullOrWhiteSpace(storagePath))
                    continue;

                if (!existingPaths.Add(storagePath))
                    continue;

                var asset = BuildArticleAsset(storagePath, attachment);
                if (asset is null)
                    continue;

                existingHook.Assets.Add(asset);
                addedAny = true;
            }

            return addedAny ? existingHook : null;
        }

        private async Task<HookM.ArticleHook?> LoadArticleHookAsync(string entryId)
        {
            try
            {
                var relative = Path.Combine("entries", entryId, "hooks", "article.json");
                var absolute = ResolveAbsolutePath(relative);
                if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
                    return null;

                var json = await File.ReadAllTextAsync(absolute).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonSerializer.Deserialize<HookM.ArticleHook>(json, HookM.JsonStd.Options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] Failed to load article hook for '{entryId}': {ex}");
                return null;
            }
        }

        private HookM.ArticleAsset? BuildArticleAsset(string storagePath, Attachment attachment)
        {
            try
            {
                var absolute = ResolveAbsolutePath(attachment.RelativePath);
                long bytes = 0;
                if (!string.IsNullOrWhiteSpace(absolute))
                {
                    try
                    {
                        var info = new FileInfo(absolute);
                        if (info.Exists)
                            bytes = info.Length;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LibraryResultsViewModel] Unable to read file info for '{absolute}': {ex}");
                    }
                }

                var fileName = Path.GetFileName(storagePath);
                var title = string.IsNullOrWhiteSpace(attachment.Title)
                    ? (fileName ?? storagePath)
                    : attachment.Title.Trim();

                return new HookM.ArticleAsset
                {
                    Title = title,
                    OriginalFilename = fileName,
                    StoragePath = storagePath,
                    Hash = TryExtractHash(storagePath),
                    ContentType = ResolveContentType(storagePath),
                    Bytes = bytes,
                    Purpose = HookM.ArticleAssetPurpose.Supplement
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] Failed to build article asset for '{storagePath}': {ex}");
                return null;
            }
        }

        private string? ResolveAbsolutePath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            try
            {
                var normalized = relativePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\', Path.DirectorySeparatorChar);
                return _workspace.GetAbsolutePath(normalized);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryResultsViewModel] Failed to resolve absolute path for '{relativePath}': {ex}");
                return null;
            }
        }

        private static string NormalizeStoragePath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            return relativePath.Replace('\', '/');
        }

        private static string TryExtractHash(string storagePath)
        {
            var fileName = Path.GetFileName(storagePath);
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(stem))
                return string.Empty;

            return stem.Length == 64 ? $"sha256-{stem}" : string.Empty;
        }

        private static string ResolveContentType(string storagePath)
        {
            var ext = Path.GetExtension(storagePath)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".rtf" => "application/rtf",
                _ => "application/octet-stream"
            };
        }
    }
}
