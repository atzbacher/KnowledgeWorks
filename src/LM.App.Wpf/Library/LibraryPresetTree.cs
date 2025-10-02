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
