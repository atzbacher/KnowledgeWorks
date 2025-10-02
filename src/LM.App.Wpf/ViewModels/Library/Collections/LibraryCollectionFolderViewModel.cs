using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.Library.Collections;

namespace LM.App.Wpf.ViewModels.Library.Collections
{
    public sealed partial class LibraryCollectionFolderViewModel : ObservableObject
    {
        public LibraryCollectionFolderViewModel(LibraryCollectionsViewModel tree,
                                                string id,
                                                string name,
                                                LibraryCollectionMetadata metadata)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            _name = name ?? string.Empty;
            Metadata = metadata ?? new LibraryCollectionMetadata();
        }

        public string Id { get; }

        [ObservableProperty]
        private string _name;

        public LibraryCollectionMetadata Metadata { get; }

        public ObservableCollection<LibraryCollectionFolderViewModel> Children { get; } = new();

        public LibraryCollectionsViewModel Tree { get; }

        [ObservableProperty]
        private int _entryCount;
    }
}
