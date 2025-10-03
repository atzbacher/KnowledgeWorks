using System.Collections.ObjectModel;
using LM.App.Wpf.Library;

namespace LM.App.Wpf.ViewModels.Library.SavedSearches
{
    public sealed partial class SavedSearchFolderViewModel : SavedSearchNodeViewModel
    {
        public SavedSearchFolderViewModel(SavedSearchTreeViewModel tree,
                                          string id,
                                          string name,
                                          int sortOrder)
            : base(tree, id, name, LibraryPresetNodeKind.Folder, sortOrder)
        {
        }

        public ObservableCollection<SavedSearchNodeViewModel> Children { get; } = new();

        /// <summary>
        /// Returns true if this folder is the invisible root container.
        /// The root folder should not be displayed as a visible tree item and cannot be dragged.
        /// </summary>
        public bool IsRoot => string.Equals(Id, LibraryPresetFolder.RootId, System.StringComparison.Ordinal);

        /// <summary>
        /// Only non-root folders can be dragged.
        /// </summary>
        public override bool IsDraggable => !IsRoot;
    }
}
