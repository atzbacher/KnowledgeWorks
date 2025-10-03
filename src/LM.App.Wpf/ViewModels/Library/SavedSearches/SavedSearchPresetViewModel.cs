using System;
using LM.App.Wpf.Common;

using LM.App.Wpf.Library;

namespace LM.App.Wpf.ViewModels.Library.SavedSearches
{
    public sealed partial class SavedSearchPresetViewModel : SavedSearchNodeViewModel
    {
        public SavedSearchPresetViewModel(SavedSearchTreeViewModel tree,
                                          LibraryFilterPreset preset,
                                          int sortOrder)
            : base(tree, preset.Id, preset.Name, LibraryPresetNodeKind.Preset, sortOrder)
        {
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        public LibraryFilterPreset Preset { get; }

        public DateTime SavedUtc => Preset.SavedUtc;

        public LibraryPresetSummary ToSummary() => new(Preset.Id, Preset.Name, Preset.SavedUtc);

        public LibraryPresetSummary Summary => ToSummary();

        /// <summary>
        /// Presets can always be dragged.
        /// </summary>
        public override bool IsDraggable => true;

    }
}
