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

namespace LM.App.Wpf.Library.Collections
{
    public sealed class LibraryCollectionStore
    {
        private const int CurrentVersion = 1;
        private readonly IWorkSpaceService _workspace;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public LibraryCollectionStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<LibraryCollectionFolder> GetHierarchyAsync(CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            Trace.WriteLine("[LibraryCollectionStore] Loaded hierarchy snapshot.");
            return root.CloneRoot();
        }

        public async Task<string> CreateFolderAsync(string? parentFolderId, string name, string createdBy, CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var parent = ResolveFolder(root, parentFolderId);

            var normalizedName = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "New Collection";
            }

            var folder = new LibraryCollectionFolder
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = normalizedName,
                Metadata = new LibraryCollectionMetadata
                {
                    CreatedBy = NormalizeUser(createdBy),
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedBy = NormalizeUser(createdBy),
                    ModifiedUtc = DateTime.UtcNow
                }
            };

            parent.Folders.Add(folder);
            parent.Metadata.ModifiedBy = NormalizeUser(createdBy);
            parent.Metadata.ModifiedUtc = DateTime.UtcNow;

            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Created folder '{folder.Name}' ({folder.Id}) under '{parent.Id}'.");
            return folder.Id;
        }

        public async Task DeleteFolderAsync(string folderId, string performedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            if (!root.TryFindFolder(folderId, out var folder, out var parent) || folder is null || parent is null)
            {
                Trace.WriteLine($"[LibraryCollectionStore] Folder '{folderId}' not found for deletion.");
                return;
            }

            parent.Folders.Remove(folder);
            parent.Metadata.ModifiedBy = NormalizeUser(performedBy);
            parent.Metadata.ModifiedUtc = DateTime.UtcNow;
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Deleted folder '{folder.Name}' ({folder.Id}).");
        }

        public async Task AddEntriesAsync(string folderId, IEnumerable<string> entryIds, string performedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                return;
            }

            if (entryIds is null)
            {
                throw new ArgumentNullException(nameof(entryIds));
            }

            var ids = entryIds
                .Select(static id => id?.Trim())
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
            {
                Trace.WriteLine("[LibraryCollectionStore] No entry ids supplied for addition.");
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            if (!root.TryFindFolder(folderId, out var folder, out _ ) || folder is null)
            {
                Trace.WriteLine($"[LibraryCollectionStore] Folder '{folderId}' not found for add operation.");
                return;
            }

            var user = NormalizeUser(performedBy);
            var now = DateTime.UtcNow;
            var added = 0;

            foreach (var id in ids)
            {
                if (folder.Entries.Any(entry => string.Equals(entry.EntryId, id, StringComparison.Ordinal)))
                {
                    continue;
                }

                folder.Entries.Add(new LibraryCollectionEntry
                {
                    EntryId = id,
                    AddedBy = user,
                    AddedUtc = now
                });

                added++;
            }

            if (added == 0)
            {
                Trace.WriteLine($"[LibraryCollectionStore] All entries already existed in folder '{folder.Name}'.");
                return;
            }

            folder.Metadata.ModifiedBy = user;
            folder.Metadata.ModifiedUtc = now;

            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Added {added} entry id(s) to folder '{folder.Name}'.");
        }

        public async Task RemoveEntriesAsync(string folderId, IEnumerable<string> entryIds, string performedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                return;
            }

            if (entryIds is null)
            {
                throw new ArgumentNullException(nameof(entryIds));
            }

            var ids = entryIds
                .Select(static id => id?.Trim())
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
            {
                Trace.WriteLine("[LibraryCollectionStore] No entry ids supplied for removal.");
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            if (!root.TryFindFolder(folderId, out var folder, out _ ) || folder is null)
            {
                Trace.WriteLine($"[LibraryCollectionStore] Folder '{folderId}' not found for remove operation.");
                return;
            }

            var removed = folder.Entries.RemoveAll(entry => ids.Contains(entry.EntryId, StringComparer.Ordinal));
            if (removed == 0)
            {
                Trace.WriteLine($"[LibraryCollectionStore] No entries removed from folder '{folder.Name}'.");
                return;
            }

            folder.Metadata.ModifiedBy = NormalizeUser(performedBy);
            folder.Metadata.ModifiedUtc = DateTime.UtcNow;

            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Removed {removed} entry id(s) from folder '{folder.Name}'.");
        }

        public async Task RenameFolderAsync(string folderId, string newName, string performedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var trimmed = newName?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                Trace.WriteLine("[LibraryCollectionStore] Ignoring rename because the new name was empty.");
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            if (!root.TryFindFolder(folderId, out var folder, out var parent) || folder is null)
            {
                Trace.WriteLine($"[LibraryCollectionStore] Cannot rename folder '{folderId}'; not found.");
                return;
            }

            folder.Name = trimmed;
            folder.Metadata.ModifiedBy = NormalizeUser(performedBy);
            folder.Metadata.ModifiedUtc = DateTime.UtcNow;
            if (parent is not null)
            {
                parent.Metadata.ModifiedBy = folder.Metadata.ModifiedBy;
                parent.Metadata.ModifiedUtc = folder.Metadata.ModifiedUtc;
            }

            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Renamed folder '{folderId}' to '{trimmed}'.");
        }

        public async Task MoveFolderAsync(string folderId, string targetFolderId, int insertIndex, string performedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            if (!root.TryFindFolder(folderId, out var folder, out var currentParent) || folder is null || currentParent is null)
            {
                Trace.WriteLine($"[LibraryCollectionStore] Cannot move folder '{folderId}'; not found.");
                return;
            }

            var destination = ResolveFolder(root, targetFolderId);
            if (ReferenceEquals(destination, folder) || IsDescendantOf(folder, destination))
            {
                Trace.WriteLine($"[LibraryCollectionStore] Ignoring move of folder '{folder.Id}' into itself or descendant.");
                return;
            }

            currentParent.Folders.Remove(folder);

            var index = Math.Clamp(insertIndex, 0, destination.Folders.Count);
            destination.Folders.Insert(index, folder);

            var user = NormalizeUser(performedBy);
            var now = DateTime.UtcNow;

            folder.Metadata.ModifiedBy = user;
            folder.Metadata.ModifiedUtc = now;
            currentParent.Metadata.ModifiedBy = user;
            currentParent.Metadata.ModifiedUtc = now;
            destination.Metadata.ModifiedBy = user;
            destination.Metadata.ModifiedUtc = now;

            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionStore] Moved folder '{folder.Id}' to '{destination.Id}' at index {index}.");
        }

        private async Task<LibraryCollectionFile> LoadAsync(CancellationToken ct)
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                Trace.WriteLine("[LibraryCollectionStore] No collection file found; creating new store.");
                return CreateDefaultFile();
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            var file = await JsonSerializer.DeserializeAsync<LibraryCollectionFile>(stream, JsonOptions, ct).ConfigureAwait(false) ?? CreateDefaultFile();
            return file;
        }

        private async Task SaveAsync(LibraryCollectionFile file, CancellationToken ct)
        {
            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            file.Version = CurrentVersion;
            var json = JsonSerializer.Serialize(file, JsonOptions);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }

        private string GetFilePath()
        {
            var root = _workspace.GetWorkspaceRoot();
            return Path.Combine(root, "library", "collections.json");
        }

        private static LibraryCollectionFile CreateDefaultFile()
        {
            return new LibraryCollectionFile
            {
                Version = CurrentVersion,
                Root = new LibraryCollectionFolder
                {
                    Id = LibraryCollectionFolder.RootId,
                    Name = "Collections",
                    Metadata = new LibraryCollectionMetadata
                    {
                        CreatedBy = NormalizeUser(Environment.UserName),
                        CreatedUtc = DateTime.UtcNow,
                        ModifiedBy = NormalizeUser(Environment.UserName),
                        ModifiedUtc = DateTime.UtcNow
                    }
                }
            };
        }

        private static LibraryCollectionFolder EnsureRoot(LibraryCollectionFile file)
        {
            if (file.Root is null)
            {
                file.Root = new LibraryCollectionFolder
                {
                    Id = LibraryCollectionFolder.RootId,
                    Name = "Collections",
                    Metadata = new LibraryCollectionMetadata
                    {
                        CreatedBy = NormalizeUser(Environment.UserName),
                        CreatedUtc = DateTime.UtcNow,
                        ModifiedBy = NormalizeUser(Environment.UserName),
                        ModifiedUtc = DateTime.UtcNow
                    }
                };
            }

            if (!string.Equals(file.Root.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                file.Root.Id = LibraryCollectionFolder.RootId;
            }

            return file.Root;
        }

        private static LibraryCollectionFolder ResolveFolder(LibraryCollectionFolder root, string? folderId)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return root;
            }

            if (root.TryFindFolder(folderId!, out var folder, out _ ) && folder is not null)
            {
                return folder;
            }

            Trace.WriteLine($"[LibraryCollectionStore] Falling back to root for missing folder '{folderId}'.");
            return root;
        }

        private static bool IsDescendantOf(LibraryCollectionFolder potentialAncestor, LibraryCollectionFolder candidate)
        {
            foreach (var child in potentialAncestor.Folders)
            {
                if (ReferenceEquals(child, candidate))
                {
                    return true;
                }

                if (IsDescendantOf(child, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeUser(string? user)
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                var environmentUser = Environment.UserName;
                return string.IsNullOrWhiteSpace(environmentUser) ? "unknown" : environmentUser;
            }

            return user.Trim();
        }
    }
}
