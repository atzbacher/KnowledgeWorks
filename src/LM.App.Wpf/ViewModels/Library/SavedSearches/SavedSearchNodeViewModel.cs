using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library.SavedSearches
{
    public abstract partial class SavedSearchNodeViewModel : ObservableObject
    {
        protected SavedSearchNodeViewModel(SavedSearchTreeViewModel tree,
                                           string id,
                                           string name,
                                           LibraryPresetNodeKind kind,
                                           int sortOrder)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Id cannot be null", nameof(id)) : id;
            this.name = name ?? string.Empty;
            Kind = kind;
            this.sortOrder = sortOrder;
        }

        public SavedSearchTreeViewModel Tree { get; }

        public string Id { get; }

        public LibraryPresetNodeKind Kind { get; }

        public SavedSearchFolderViewModel? Parent { get; internal set; }

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private int sortOrder;
    }
}
