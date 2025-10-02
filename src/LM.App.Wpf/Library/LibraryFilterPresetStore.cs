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

namespace LM.App.Wpf.Library
{
    /// <summary>
    /// Persists named Library filter presets under the active workspace.
    /// </summary>
    public sealed class LibraryFilterPresetStore
    {
        private const int CurrentVersion = 2;
        private readonly IWorkSpaceService _workspace;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };

        public LibraryFilterPresetStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public Task SavePresetAsync(LibraryFilterPreset preset, CancellationToken ct = default)
        {
            return SavePresetAsync(preset, LibraryPresetFolder.RootId, ct);
        }

        public async Task SavePresetAsync(LibraryFilterPreset preset, string? targetFolderId, CancellationToken ct = default)
        {
            if (preset is null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var destination = ResolveFolder(root, targetFolderId);

            if (string.IsNullOrWhiteSpace(preset.Id))
            {
                preset.Id = Guid.NewGuid().ToString("N");
            }

            preset.SavedUtc = DateTime.UtcNow;

            var (existingParent, existingPreset) = TryFindPreset(root, preset.Id);
            if (existingPreset is null && !string.IsNullOrWhiteSpace(preset.Name))
            {
                (existingParent, existingPreset) = TryFindPresetByName(root, preset.Name);
            }

            if (existingPreset is not null)
            {
                existingParent!.Presets.Remove(existingPreset);
                Trace.WriteLine($"[LibraryFilterPresetStore] Updating preset '{preset.Name}' ({preset.Id}).");
            }

            InsertPreset(destination, preset, existingPreset?.SortOrder ?? destination.EnumerateChildren().Count());
            NormalizeTree(root);

            file.Version = CurrentVersion;
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Persisted preset '{preset.Name}' to folder '{destination.Id}'.");
        }

        public async Task<IReadOnlyList<LibraryFilterPreset>> ListPresetsAsync(CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var results = new List<LibraryFilterPreset>();
            Flatten(root, results);
            Trace.WriteLine($"[LibraryFilterPresetStore] Listed {results.Count} preset(s).");
            return results.Select(static preset => preset.Clone()).ToArray();
        }

        public async Task<LibraryPresetFolder> GetHierarchyAsync(CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            Trace.WriteLine("[LibraryFilterPresetStore] Loaded hierarchy snapshot.");
            return root.Clone();
        }

        public async Task<LibraryFilterPreset?> TryGetPresetByIdAsync(string presetId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return null;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (_, preset) = TryFindPreset(root, presetId);
            return preset?.Clone();
        }

        public async Task<LibraryFilterPreset?> TryGetPresetAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (_, presetById) = TryFindPreset(root, key);
            if (presetById is not null)
            {
                return presetById.Clone();
            }

            var (_, preset) = TryFindPresetByName(root, key);
            return preset?.Clone();
        }

        public async Task DeletePresetAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (parent, preset) = TryFindPreset(root, key);
            if (preset is null)
            {
                (parent, preset) = TryFindPresetByName(root, key);
            }

            if (preset is null || parent is null)
            {
                Trace.WriteLine($"[LibraryFilterPresetStore] No preset found for key '{key}'.");
                return;
            }

            parent.Presets.Remove(preset);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Deleted preset '{preset.Name}' ({preset.Id}).");
        }

        public async Task<string> CreateFolderAsync(string parentFolderId, string name, CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var parent = ResolveFolder(root, parentFolderId);

            var folder = new LibraryPresetFolder
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name?.Trim() ?? string.Empty
            };

            InsertFolder(parent, folder, parent.EnumerateChildren().Count());
            NormalizeTree(root);
            file.Version = CurrentVersion;
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Created folder '{folder.Name}' ({folder.Id}) under '{parent.Id}'.");
            return folder.Id;
        }

        public async Task DeleteFolderAsync(string folderId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryPresetFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (parent, folder) = TryFindFolder(root, folderId);
            if (folder is null || parent is null)
            {
                Trace.WriteLine($"[LibraryFilterPresetStore] Folder '{folderId}' not found for deletion.");
                return;
            }

            parent.Folders.Remove(folder);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Deleted folder '{folder.Name}' ({folder.Id}).");
        }

        public async Task MovePresetAsync(string presetId, string targetFolderId, int insertIndex, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (currentParent, preset) = TryFindPreset(root, presetId);
            if (preset is null || currentParent is null)
            {
                Trace.WriteLine($"[LibraryFilterPresetStore] Cannot move preset '{presetId}'; not found.");
                return;
            }

            var destination = ResolveFolder(root, targetFolderId);
            var ordered = currentParent.EnumerateChildren().ToList();
            var removed = ordered.FindIndex(item => item.Kind == LibraryPresetNodeKind.Preset && item.Preset is not null && ReferenceEquals(item.Preset, preset));
            if (removed >= 0)
            {
                ordered.RemoveAt(removed);
                ReassignChildren(currentParent, ordered);
            }

            InsertPreset(destination, preset, insertIndex);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Moved preset '{preset.Name}' to folder '{destination.Id}' at index {insertIndex}.");
        }

        public async Task MoveFolderAsync(string folderId, string targetFolderId, int insertIndex, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryPresetFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var root = EnsureRoot(file);
            var (currentParent, folder) = TryFindFolder(root, folderId);
            if (folder is null || currentParent is null)
            {
                Trace.WriteLine($"[LibraryFilterPresetStore] Cannot move folder '{folderId}'; not found.");
                return;
            }

            var destination = ResolveFolder(root, targetFolderId);
            if (ReferenceEquals(destination, folder) || IsDescendant(folder, destination))
            {
                Trace.WriteLine($"[LibraryFilterPresetStore] Ignoring move of folder '{folder.Id}' into itself or descendant.");
                return;
            }

            var ordered = currentParent.EnumerateChildren().ToList();
            var removed = ordered.FindIndex(item => item.Kind == LibraryPresetNodeKind.Folder && item.Folder is not null && ReferenceEquals(item.Folder, folder));
            if (removed >= 0)
            {
                ordered.RemoveAt(removed);
                ReassignChildren(currentParent, ordered);
            }

            InsertFolder(destination, folder, insertIndex);
            NormalizeTree(root);
            await SaveAsync(file, ct).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryFilterPresetStore] Moved folder '{folder.Name}' to '{destination.Id}' at index {insertIndex}.");
        }

        private async Task<LibraryPresetFile> LoadAsync(CancellationToken ct)
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                Trace.WriteLine("[LibraryFilterPresetStore] No preset file found; creating new store.");
                return CreateDefaultFile();
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            var file = await JsonSerializer.DeserializeAsync<LibraryPresetFile>(stream, JsonOptions, ct).ConfigureAwait(false) ?? CreateDefaultFile();
            MigrateIfNeeded(file);
            return file;
        }

        private async Task SaveAsync(LibraryPresetFile file, CancellationToken ct)
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
            return Path.Combine(root, "library", "filter-presets.json");
        }

        private static LibraryPresetFile CreateDefaultFile()
        {
            return new LibraryPresetFile
            {
                Version = CurrentVersion,
                Root = new LibraryPresetFolder
                {
                    Id = LibraryPresetFolder.RootId,
                    Name = string.Empty
                }
            };
        }

        private static void MigrateIfNeeded(LibraryPresetFile file)
        {
            if (file.Root is null)
            {
                file.Root = new LibraryPresetFolder
                {
                    Id = LibraryPresetFolder.RootId,
                    Name = string.Empty
                };
            }

            if (file.Version >= 2)
            {
                NormalizeTree(file.Root);
                return;
            }

            if (file.LegacyPresets is not null && file.LegacyPresets.Count > 0)
            {
                var index = 0;
                foreach (var preset in file.LegacyPresets)
                {
                    if (string.IsNullOrWhiteSpace(preset.Id))
                    {
                        preset.Id = Guid.NewGuid().ToString("N");
                    }

                    preset.SortOrder = index++;
                    file.Root.Presets.Add(preset);
                }
            }

            file.LegacyPresets = null;
            NormalizeTree(file.Root);
        }

        private static LibraryPresetFolder EnsureRoot(LibraryPresetFile file)
        {
            if (file.Root is null)
            {
                file.Root = new LibraryPresetFolder
                {
                    Id = LibraryPresetFolder.RootId,
                    Name = string.Empty
                };
            }

            if (!string.Equals(file.Root.Id, LibraryPresetFolder.RootId, StringComparison.Ordinal))
            {
                file.Root.Id = LibraryPresetFolder.RootId;
            }

            return file.Root;
        }

        private static LibraryPresetFolder ResolveFolder(LibraryPresetFolder root, string? folderId)
        {
            if (string.IsNullOrWhiteSpace(folderId) || string.Equals(folderId, LibraryPresetFolder.RootId, StringComparison.Ordinal))
            {
                return root;
            }

            var (_, folder) = TryFindFolder(root, folderId);
            return folder ?? root;
        }

        private static (LibraryPresetFolder? Parent, LibraryFilterPreset? Preset) TryFindPreset(LibraryPresetFolder root, string presetId)
        {
            foreach (var item in root.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LibraryPresetNodeKind.Preset when item.Preset is not null && string.Equals(item.Preset.Id, presetId, StringComparison.Ordinal):
                        return (root, item.Preset);
                    case LibraryPresetNodeKind.Folder when item.Folder is not null:
                    {
                        var found = TryFindPreset(item.Folder, presetId);
                        if (found.Preset is not null)
                        {
                            return found;
                        }

                        break;
                    }
                }
            }

            return (null, null);
        }

        private static (LibraryPresetFolder? Parent, LibraryFilterPreset? Preset) TryFindPresetByName(LibraryPresetFolder root, string name)
        {
            foreach (var item in root.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LibraryPresetNodeKind.Preset when item.Preset is not null && string.Equals(item.Preset.Name, name, StringComparison.OrdinalIgnoreCase):
                        return (root, item.Preset);
                    case LibraryPresetNodeKind.Folder when item.Folder is not null:
                    {
                        var found = TryFindPresetByName(item.Folder, name);
                        if (found.Preset is not null)
                        {
                            return found;
                        }

                        break;
                    }
                }
            }

            return (null, null);
        }

        private static (LibraryPresetFolder? Parent, LibraryPresetFolder? Folder) TryFindFolder(LibraryPresetFolder root, string folderId)
        {
            foreach (var item in root.EnumerateChildren())
            {
                if (item.Kind == LibraryPresetNodeKind.Folder && item.Folder is not null)
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

        private static void InsertPreset(LibraryPresetFolder destination, LibraryFilterPreset preset, int insertIndex)
        {
            var ordered = destination.EnumerateChildren().ToList();
            insertIndex = Math.Clamp(insertIndex, 0, ordered.Count);
            ordered.Insert(insertIndex, new LibraryPresetTreeItem(LibraryPresetNodeKind.Preset, insertIndex, null, preset));
            ReassignChildren(destination, ordered);
        }

        private static void InsertFolder(LibraryPresetFolder destination, LibraryPresetFolder folder, int insertIndex)
        {
            var ordered = destination.EnumerateChildren().ToList();
            insertIndex = Math.Clamp(insertIndex, 0, ordered.Count);
            ordered.Insert(insertIndex, new LibraryPresetTreeItem(LibraryPresetNodeKind.Folder, insertIndex, folder, null));
            ReassignChildren(destination, ordered);
        }

        private static void ReassignChildren(LibraryPresetFolder folder, List<LibraryPresetTreeItem> ordered)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                switch (ordered[i].Kind)
                {
                    case LibraryPresetNodeKind.Folder when ordered[i].Folder is not null:
                        ordered[i].Folder.SortOrder = i;
                        break;
                    case LibraryPresetNodeKind.Preset when ordered[i].Preset is not null:
                        ordered[i].Preset.SortOrder = i;
                        break;
                }
            }

            folder.Folders = ordered
                .Where(static item => item.Kind == LibraryPresetNodeKind.Folder && item.Folder is not null)
                .Select(static item => item.Folder!)
                .OrderBy(static f => f.SortOrder)
                .ToList();

            folder.Presets = ordered
                .Where(static item => item.Kind == LibraryPresetNodeKind.Preset && item.Preset is not null)
                .Select(static item => item.Preset!)
                .OrderBy(static p => p.SortOrder)
                .ToList();
        }

        private static void NormalizeTree(LibraryPresetFolder root)
        {
            var folders = root.Folders ?? new List<LibraryPresetFolder>();
            root.Folders = folders;

            var presets = root.Presets ?? new List<LibraryFilterPreset>();
            root.Presets = presets;

            if (string.IsNullOrWhiteSpace(root.Id))
            {
                root.Id = Guid.NewGuid().ToString("N");
            }

            if (string.Equals(root.Id, LibraryPresetFolder.RootId, StringComparison.Ordinal))
            {
                root.Id = LibraryPresetFolder.RootId;
            }

            var ordered = root.EnumerateChildren().ToList();
            ReassignChildren(root, ordered);

            folders = root.Folders ?? folders;
            presets = root.Presets ?? presets;

            foreach (var folder in folders.ToArray())
            {
                NormalizeTree(folder);
            }

            foreach (var preset in presets)
            {
                if (string.IsNullOrWhiteSpace(preset.Id))
                {
                    preset.Id = Guid.NewGuid().ToString("N");
                }
            }
        }

        private static bool IsDescendant(LibraryPresetFolder potentialAncestor, LibraryPresetFolder candidate)
        {
            if (ReferenceEquals(potentialAncestor, candidate))
            {
                return true;
            }

            foreach (var folder in potentialAncestor.Folders)
            {
                if (ReferenceEquals(folder, candidate))
                {
                    return true;
                }

                if (IsDescendant(folder, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Flatten(LibraryPresetFolder folder, List<LibraryFilterPreset> results)
        {
            foreach (var item in folder.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LibraryPresetNodeKind.Preset when item.Preset is not null:
                        results.Add(item.Preset);
                        break;
                    case LibraryPresetNodeKind.Folder when item.Folder is not null:
                        Flatten(item.Folder, results);
                        break;
                }
            }
        }

        private sealed class LibraryPresetFile
        {
            public int Version { get; set; }

            public LibraryPresetFolder? Root { get; set; }

            [JsonPropertyName("presets")]
            public List<LibraryFilterPreset>? LegacyPresets { get; set; }
        }
    }

    public sealed class LibraryFilterPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

        public LibraryFilterState State { get; set; } = new();

        internal LibraryFilterPreset Clone()
            => new()
            {
                Id = Id,
                Name = Name,
                SortOrder = SortOrder,
                SavedUtc = SavedUtc,
                State = State.Clone()
            };
    }

    public sealed class LibraryFilterState
    {
        public bool UseFullTextSearch { get; set; }
        public string? UnifiedQuery { get; set; }
        public string? FullTextQuery { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? SortKey { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();

        internal LibraryFilterState Clone()
        {
            var clone = (LibraryFilterState)MemberwiseClone();
            clone.Tags = Tags?.ToArray() ?? Array.Empty<string>();
            return clone;
        }
    }
}
