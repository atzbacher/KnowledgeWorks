using System;
using System.Collections.Generic;
using System.Linq;

namespace LM.App.Wpf.Library
{
    public enum LibraryPresetNodeKind
    {
        Folder,
        Preset
    }

    public sealed class LibraryPresetFolder
    {
        public const string RootId = "root";

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<LibraryPresetFolder> Folders { get; set; } = new();

        public List<LibraryFilterPreset> Presets { get; set; } = new();

        public IEnumerable<LibraryPresetTreeItem> EnumerateChildren()
        {
            var combined = new List<LibraryPresetTreeItem>();

            foreach (var folder in Folders ?? Enumerable.Empty<LibraryPresetFolder>())
            {
                if (folder is null)
                {
                    continue;
                }

                combined.Add(new LibraryPresetTreeItem(LibraryPresetNodeKind.Folder, folder.SortOrder, folder, null));
            }

            foreach (var preset in Presets ?? Enumerable.Empty<LibraryFilterPreset>())
            {
                if (preset is null)
                {
                    continue;
                }

                combined.Add(new LibraryPresetTreeItem(LibraryPresetNodeKind.Preset, preset.SortOrder, null, preset));
            }

            return combined
                .OrderBy(static item => item.SortOrder)
                .ToArray();
        }

        public LibraryPresetFolder Clone()
        {
            var clone = new LibraryPresetFolder
            {
                Id = Id,
                Name = Name,
                SortOrder = SortOrder
            };

            foreach (var folder in Folders ?? Enumerable.Empty<LibraryPresetFolder>())
            {
                if (folder is null)
                {
                    continue;
                }

                clone.Folders.Add(folder.Clone());
            }

            foreach (var preset in Presets ?? Enumerable.Empty<LibraryFilterPreset>())
            {
                if (preset is null)
                {
                    continue;
                }

                clone.Presets.Add(preset.Clone());
            }

            return clone;
        }

        internal void NormalizeOrder()
        {
            var ordered = EnumerateChildren().ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var item = ordered[i];

                if (item.Kind == LibraryPresetNodeKind.Folder && item.Folder is not null)
                {
                    item.Folder.SortOrder = i;
                    continue;
                }

                if (item.Kind == LibraryPresetNodeKind.Preset && item.Preset is not null)
                {
                    item.Preset.SortOrder = i;
                }
            }
        }
    }

    public readonly struct LibraryPresetTreeItem
    {
        public LibraryPresetTreeItem(LibraryPresetNodeKind kind, int sortOrder, LibraryPresetFolder? folder, LibraryFilterPreset? preset)
        {
            Kind = kind;
            SortOrder = sortOrder;
            Folder = folder;
            Preset = preset;
        }

        public LibraryPresetNodeKind Kind { get; }

        public int SortOrder { get; }

        public LibraryPresetFolder? Folder { get; }

        public LibraryFilterPreset? Preset { get; }
    }
}
