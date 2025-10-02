using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;

namespace LM.App.Wpf.Library.Collections
{
    public sealed class LibraryCollectionFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("root")]
        public LibraryCollectionFolder? Root { get; set; }
    }

    public sealed class LibraryCollectionFolder
    {
        public const string RootId = "collections-root";

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public LibraryCollectionMetadata Metadata { get; set; } = new();

        [JsonPropertyName("folders")]
        public List<LibraryCollectionFolder> Folders { get; set; } = new();

        [JsonPropertyName("entries")]
        public List<LibraryCollectionEntry> Entries { get; set; } = new();

        public LibraryCollectionFolder Clone()
        {
            return new LibraryCollectionFolder
            {
                Id = Id,
                Name = Name,
                Metadata = Metadata.Clone(),
                Folders = Folders.Select(static folder => folder.Clone()).ToList(),
                Entries = Entries.Select(static entry => entry.Clone()).ToList()
            };
        }

        public IEnumerable<LibraryCollectionFolder> EnumerateDescendants()
        {
            foreach (var child in Folders)
            {
                yield return child;

                foreach (var grandChild in child.EnumerateDescendants())
                {
                    yield return grandChild;
                }
            }
        }
    }

    public sealed class LibraryCollectionMetadata
    {
        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("modifiedBy")]
        public string ModifiedBy { get; set; } = string.Empty;

        [JsonPropertyName("modifiedUtc")]
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

        public LibraryCollectionMetadata Clone()
        {
            return new LibraryCollectionMetadata
            {
                CreatedBy = CreatedBy,
                CreatedUtc = CreatedUtc,
                ModifiedBy = ModifiedBy,
                ModifiedUtc = ModifiedUtc
            };
        }
    }

    public sealed class LibraryCollectionEntry
    {
        [JsonPropertyName("entryId")]
        public string EntryId { get; set; } = string.Empty;

        [JsonPropertyName("addedBy")]
        public string AddedBy { get; set; } = string.Empty;

        [JsonPropertyName("addedUtc")]
        public DateTime AddedUtc { get; set; } = DateTime.UtcNow;

        public LibraryCollectionEntry Clone()
        {
            return new LibraryCollectionEntry
            {
                EntryId = EntryId,
                AddedBy = AddedBy,
                AddedUtc = AddedUtc
            };
        }
    }

    internal static class LibraryCollectionFolderExtensions
    {
        public static LibraryCollectionFolder CloneRoot(this LibraryCollectionFolder root)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return root.Clone();
        }

        public static bool TryFindFolder(this LibraryCollectionFolder root, string folderId, [NotNullWhen(true)] out LibraryCollectionFolder? folder, [NotNullWhen(true)] out LibraryCollectionFolder? parent)
        {
            folder = null;
            parent = null;

            if (root is null)
            {
                Trace.WriteLine($"[LibraryCollectionFolderExtensions] Cannot locate folder '{folderId}' because the root folder is null.");
                return false;
            }

            var pending = new Stack<(LibraryCollectionFolder Node, LibraryCollectionFolder Parent)>();
            pending.Push((root, root));

            while (pending.Count > 0)
            {
                var (current, currentParent) = pending.Pop();

                if (string.Equals(current.Id, folderId, StringComparison.Ordinal))
                {
                    folder = current;
                    parent = currentParent;
                    Trace.WriteLine($"[LibraryCollectionFolderExtensions] Found folder '{folderId}' with parent '{currentParent.Id}'.");
                    return true;
                }

                foreach (var child in current.Folders)
                {
                    pending.Push((child, current));
                }
            }

            Trace.WriteLine($"[LibraryCollectionFolderExtensions] Folder '{folderId}' not found starting from root '{root.Id}'.");
            return false;
        }
    }
}
