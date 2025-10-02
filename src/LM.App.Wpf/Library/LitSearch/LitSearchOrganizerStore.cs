using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.App.Wpf.Library.LitSearch
{
    internal static class LitSearchOrganizerSchema
    {
        internal const int CurrentVersion = 1;
    }

    /// <summary>
    /// Persists user-defined organization for litsearch entries.
    /// </summary>
    public sealed class LitSearchOrganizerStore
    {
        private readonly IWorkSpaceService _workspace;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };

        public LitSearchOrganizerStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<LitSearchOrganizerFolder> SyncEntriesAsync(IEnumerable<string> entryIds, CancellationToken ct = default)
        {
            if (entryIds is null)
            {
                throw new ArgumentNullException(nameof(entryIds));
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var actual = new HashSet<string>(entryIds.Where(static id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);

            var changed = RemoveMissingEntries(root, actual);
            changed |= AddMissingEntries(root, actual);

            if (changed)
            {
                NormalizeTree(root);
                file.Version = LitSearchOrganizerSchema.CurrentVersion;
                await SaveAsync(file, ct).ConfigureAwait(false);
                Trace.WriteLine($"[LitSearchOrganizerStore] Synced litsearch entries. Total tracked: {EnumerateEntries(root).Count()}.");
            }

            return root.Clone();
        }

        public async Task<LitSearchOrganizerFolder> GetHierarchyAsync(CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            return root.Clone();
        }

        public async Task<string> CreateFolderAsync(string parentFolderId, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Folder name must be provided.", nameof(name));
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var parent = ResolveFolder(root, parentFolderId);

            var folder = new LitSearchOrganizerFolder
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name.Trim()
            };

            InsertFolder(parent, folder, parent.EnumerateChildren().Count());
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchOrganizerStore] Created folder '{folder.Name}' ({folder.Id}) under '{parent.Id}'.");
            return folder.Id;
        }

        public async Task DeleteFolderAsync(string folderId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LitSearchOrganizerFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (_, folder) = TryFindFolder(root, folderId);
            if (folder is null || parent is null)
            {
                Trace.WriteLine($"[LitSearchOrganizerStore] Folder '{folderId}' not found for deletion.");
                return;
            }

            var entryIds = EnumerateEntries(folder).Select(static entry => entry.EntryId).ToArray();
            parent.Folders.Remove(folder);

            foreach (var entryId in entryIds)
            {
                if (string.IsNullOrWhiteSpace(entryId))
                {
                    continue;
                }

                InsertEntry(parent, new LitSearchOrganizerEntry { EntryId = entryId }, parent.EnumerateChildren().Count());
            }

            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchOrganizerStore] Deleted folder '{folder.Name}' ({folder.Id}). Moved {entryIds.Length} entrie(s) to '{parent.Id}'.");
        }

        public async Task RenameFolderAsync(string folderId, string newName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LitSearchOrganizerFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var trimmed = newName?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                Trace.WriteLine("[LitSearchOrganizerStore] Ignoring rename because the new name was empty.");
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (_, folder) = TryFindFolder(root, folderId);
            if (folder is null)
            {
                Trace.WriteLine($"[LitSearchOrganizerStore] Cannot rename folder '{folderId}'; not found.");
                return;
            }

            folder.Name = trimmed;
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchOrganizerStore] Renamed folder '{folderId}' to '{trimmed}'.");
        }

        public async Task MoveEntryAsync(string entryId, string targetFolderId, int insertIndex, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (currentParent, entry) = TryFindEntry(root, entryId);
            if (entry is null || currentParent is null)
            {
                Trace.WriteLine($"[LitSearchOrganizerStore] Entry '{entryId}' not tracked; adding to target '{targetFolderId}'.");
                entry = new LitSearchOrganizerEntry { EntryId = entryId };
            }
            else
            {
                currentParent.Entries.Remove(entry);
            }

            var destination = ResolveFolder(root, targetFolderId);
            InsertEntry(destination, entry, insertIndex);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchOrganizerStore] Moved entry '{entryId}' to folder '{destination.Id}' at index {insertIndex}.");
        }

        public async Task MoveFolderAsync(string folderId, string targetFolderId, int insertIndex, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LitSearchOrganizerFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (currentParent, folder) = TryFindFolder(root, folderId);
            if (folder is null || currentParent is null)
            {
                Trace.WriteLine($"[LitSearchOrganizerStore] Cannot move folder '{folderId}'; not found.");
                return;
            }

            var destination = ResolveFolder(root, targetFolderId);
            if (ReferenceEquals(destination, folder) || IsDescendant(folder, destination))
            {
                Trace.WriteLine($"[LitSearchOrganizerStore] Ignoring move of folder '{folder.Id}' into itself or descendant.");
                return;
            }

            var ordered = currentParent.EnumerateChildren().ToList();
            var removedIndex = ordered.FindIndex(item => item.Kind == LitSearchOrganizerNodeKind.Folder && ReferenceEquals(item.Folder, folder));
            if (removedIndex >= 0)
            {
                ordered.RemoveAt(removedIndex);
                ReassignChildren(currentParent, ordered);
            }

            InsertFolder(destination, folder, insertIndex);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchOrganizerStore] Moved folder '{folder.Id}' to '{destination.Id}' at index {insertIndex}.");
        }

        private async Task<LitSearchOrganizerFile> LoadAsync(CancellationToken ct)
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                Trace.WriteLine("[LitSearchOrganizerStore] No organizer file found; creating new store.");
                return CreateDefaultFile();
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            var file = await JsonSerializer.DeserializeAsync<LitSearchOrganizerFile>(stream, JsonOptions, ct).ConfigureAwait(false) ?? CreateDefaultFile();
            EnsureRoot(file);
            NormalizeTree(file.Root!);
            return file;
        }

        private async Task SaveAsync(LitSearchOrganizerFile file, CancellationToken ct)
        {
            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            file.Version = LitSearchOrganizerSchema.CurrentVersion;
            var json = JsonSerializer.Serialize(file, JsonOptions);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }

        private string GetFilePath()
        {
            var root = _workspace.GetWorkspaceRoot();
            return Path.Combine(root, "library", "litsearch-organizer.json");
        }

        private static LitSearchOrganizerFile CreateDefaultFile()
        {
            return new LitSearchOrganizerFile
            {
                Version = LitSearchOrganizerSchema.CurrentVersion,
                Root = new LitSearchOrganizerFolder
                {
                    Id = LitSearchOrganizerFolder.RootId,
                    Name = string.Empty
                }
            };
        }

        private static LitSearchOrganizerFolder EnsureRoot(LitSearchOrganizerFile file)
        {
            if (file.Root is null)
            {
                file.Root = new LitSearchOrganizerFolder
                {
                    Id = LitSearchOrganizerFolder.RootId,
                    Name = string.Empty
                };
            }

            if (!string.Equals(file.Root.Id, LitSearchOrganizerFolder.RootId, StringComparison.Ordinal))
            {
                file.Root.Id = LitSearchOrganizerFolder.RootId;
            }

            file.Root.Name ??= string.Empty;
            file.Root.Folders ??= new List<LitSearchOrganizerFolder>();
            file.Root.Entries ??= new List<LitSearchOrganizerEntry>();
            return file.Root;
        }

        private static bool RemoveMissingEntries(LitSearchOrganizerFolder folder, HashSet<string> actual)
        {
            var changed = false;
            if (folder.Entries is null)
            {
                folder.Entries = new List<LitSearchOrganizerEntry>();
            }

            for (var i = folder.Entries.Count - 1; i >= 0; i--)
            {
                var entry = folder.Entries[i];
                if (string.IsNullOrWhiteSpace(entry.EntryId) || !actual.Contains(entry.EntryId))
                {
                    folder.Entries.RemoveAt(i);
                    changed = true;
                }
            }

            if (folder.Folders is null)
            {
                folder.Folders = new List<LitSearchOrganizerFolder>();
            }

            for (var i = folder.Folders.Count - 1; i >= 0; i--)
            {
                var child = folder.Folders[i];
                if (RemoveMissingEntries(child, actual))
                {
                    changed = true;
                }

                if (EnumerateEntries(child).Any())
                {
                    continue;
                }

                if (child.Folders.Count == 0 && child.Entries.Count == 0)
                {
                    folder.Folders.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool AddMissingEntries(LitSearchOrganizerFolder root, HashSet<string> actual)
        {
            var existing = new HashSet<string>(EnumerateEntries(root).Select(static entry => entry.EntryId), StringComparer.Ordinal);
            var added = false;

            foreach (var entryId in actual)
            {
                if (existing.Contains(entryId))
                {
                    continue;
                }

                root.Entries.Add(new LitSearchOrganizerEntry
                {
                    EntryId = entryId,
                    SortOrder = root.EnumerateChildren().Count()
                });
                added = true;
            }

            return added;
        }

        private static (LitSearchOrganizerFolder? Parent, LitSearchOrganizerFolder? Folder) TryFindFolder(LitSearchOrganizerFolder root, string folderId)
        {
            foreach (var item in root.EnumerateChildren())
            {
                if (item.Kind == LitSearchOrganizerNodeKind.Folder && item.Folder is not null)
                {
                    if (string.Equals(item.Folder.Id, folderId, StringComparison.Ordinal))
                    {
                        return (root, item.Folder);
                    }

                    var nested = TryFindFolder(item.Folder, folderId);
                    if (nested.Folder is not null)
                    {
                        return nested;
                    }
                }
            }

            return (null, null);
        }

        private static (LitSearchOrganizerFolder? Parent, LitSearchOrganizerEntry? Entry) TryFindEntry(LitSearchOrganizerFolder root, string entryId)
        {
            foreach (var item in root.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LitSearchOrganizerNodeKind.Entry when item.Entry is not null && string.Equals(item.Entry.EntryId, entryId, StringComparison.Ordinal):
                        return (root, item.Entry);
                    case LitSearchOrganizerNodeKind.Folder when item.Folder is not null:
                    {
                        var nested = TryFindEntry(item.Folder, entryId);
                        if (nested.Entry is not null)
                        {
                            return nested;
                        }

                        break;
                    }
                }
            }

            return (null, null);
        }

        private static LitSearchOrganizerFolder ResolveFolder(LitSearchOrganizerFolder root, string? folderId)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LitSearchOrganizerFolder.RootId, StringComparison.Ordinal))
            {
                return root;
            }

            var (_, folder) = TryFindFolder(root, folderId);
            return folder ?? root;
        }

        private static bool IsDescendant(LitSearchOrganizerFolder ancestor, LitSearchOrganizerFolder candidate)
        {
            if (ReferenceEquals(ancestor, candidate))
            {
                return true;
            }

            foreach (var item in ancestor.EnumerateChildren())
            {
                if (item.Kind == LitSearchOrganizerNodeKind.Folder && item.Folder is not null)
                {
                    if (IsDescendant(item.Folder, candidate))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<LitSearchOrganizerEntry> EnumerateEntries(LitSearchOrganizerFolder folder)
        {
            foreach (var entry in folder.Entries)
            {
                yield return entry;
            }

            foreach (var child in folder.Folders)
            {
                foreach (var nested in EnumerateEntries(child))
                {
                    yield return nested;
                }
            }
        }

        private static void InsertEntry(LitSearchOrganizerFolder destination, LitSearchOrganizerEntry entry, int insertIndex)
        {
            var ordered = destination.EnumerateChildren().ToList();
            insertIndex = Math.Clamp(insertIndex, 0, ordered.Count);
            ordered.Insert(insertIndex, new LitSearchOrganizerTreeItem(LitSearchOrganizerNodeKind.Entry, entry, null));
            ReassignChildren(destination, ordered);
        }

        private static void InsertFolder(LitSearchOrganizerFolder destination, LitSearchOrganizerFolder folder, int insertIndex)
        {
            var ordered = destination.EnumerateChildren().ToList();
            insertIndex = Math.Clamp(insertIndex, 0, ordered.Count);
            ordered.Insert(insertIndex, new LitSearchOrganizerTreeItem(LitSearchOrganizerNodeKind.Folder, null, folder));
            ReassignChildren(destination, ordered);
        }

        private static void ReassignChildren(LitSearchOrganizerFolder folder, List<LitSearchOrganizerTreeItem> ordered)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var item = ordered[i];

                if (item.Kind == LitSearchOrganizerNodeKind.Folder && item.Folder is not null)
                {
                    item.Folder.SortOrder = i;
                    continue;
                }

                if (item.Kind == LitSearchOrganizerNodeKind.Entry && item.Entry is not null)
                {
                    item.Entry.SortOrder = i;
                }
            }

            folder.Folders = ordered
                .Where(static item => item.Kind == LitSearchOrganizerNodeKind.Folder && item.Folder is not null)
                .Select(static item => item.Folder!)
                .OrderBy(static f => f.SortOrder)
                .ToList();

            folder.Entries = ordered
                .Where(static item => item.Kind == LitSearchOrganizerNodeKind.Entry && item.Entry is not null)
                .Select(static item => item.Entry!)
                .OrderBy(static e => e.SortOrder)
                .ToList();
        }

        private static void NormalizeTree(LitSearchOrganizerFolder folder)
        {
            folder.Folders ??= new List<LitSearchOrganizerFolder>();
            folder.Entries ??= new List<LitSearchOrganizerEntry>();

            if (string.IsNullOrWhiteSpace(folder.Id))
            {
                folder.Id = Guid.NewGuid().ToString("N");
            }

            var ordered = folder.EnumerateChildren().ToList();
            ReassignChildren(folder, ordered);

            foreach (var child in folder.Folders.ToArray())
            {
                NormalizeTree(child);
            }
        }
    }

    internal enum LitSearchOrganizerNodeKind
    {
        Folder,
        Entry
    }

    public sealed class LitSearchOrganizerFolder
    {
        public const string RootId = "root";

        [JsonPropertyName("id")]
        public string Id { get; set; } = RootId;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; }

        [JsonPropertyName("folders")]
        public List<LitSearchOrganizerFolder> Folders { get; set; } = new();

        [JsonPropertyName("entries")]
        public List<LitSearchOrganizerEntry> Entries { get; set; } = new();

        internal IEnumerable<LitSearchOrganizerTreeItem> EnumerateChildren()
        {
            foreach (var folder in Folders)
            {
                yield return new LitSearchOrganizerTreeItem(LitSearchOrganizerNodeKind.Folder, null, folder);
            }

            foreach (var entry in Entries)
            {
                yield return new LitSearchOrganizerTreeItem(LitSearchOrganizerNodeKind.Entry, entry, null);
            }
        }

        public LitSearchOrganizerFolder Clone()
        {
            return new LitSearchOrganizerFolder
            {
                Id = Id,
                Name = Name,
                SortOrder = SortOrder,
                Folders = Folders.Select(static folder => folder.Clone()).ToList(),
                Entries = Entries.Select(static entry => new LitSearchOrganizerEntry
                {
                    EntryId = entry.EntryId,
                    SortOrder = entry.SortOrder
                }).ToList()
            };
        }
    }

    public sealed class LitSearchOrganizerEntry
    {
        [JsonPropertyName("entryId")]
        public string EntryId { get; set; } = string.Empty;

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; }
    }

    internal sealed class LitSearchOrganizerTreeItem
    {
        public LitSearchOrganizerTreeItem(LitSearchOrganizerNodeKind kind, LitSearchOrganizerEntry? entry, LitSearchOrganizerFolder? folder)
        {
            Kind = kind;
            Entry = entry;
            Folder = folder;
        }

        public LitSearchOrganizerNodeKind Kind { get; }

        public LitSearchOrganizerEntry? Entry { get; }

        public LitSearchOrganizerFolder? Folder { get; }
    }

    internal sealed class LitSearchOrganizerFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = LitSearchOrganizerSchema.CurrentVersion;

        [JsonPropertyName("root")]
        public LitSearchOrganizerFolder? Root { get; set; }
    }
}
