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
    }
}
