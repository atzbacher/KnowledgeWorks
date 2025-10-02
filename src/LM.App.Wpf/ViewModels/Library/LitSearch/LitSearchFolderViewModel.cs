using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Library.LitSearch
{
    public sealed partial class LitSearchFolderViewModel : LitSearchNodeViewModel
    {
        public LitSearchFolderViewModel(LitSearchTreeViewModel tree, string id, string name, bool isRoot)
            : base(tree)
        {
            Id = id;
            nameInternal = name;
            IsRoot = isRoot;
        }

        public override string Id { get; }

        public override string Name => NameInternal;

        [ObservableProperty]
        private string nameInternal;

        public override bool IsDraggable => !IsRoot;

        public bool IsRoot { get; }

        public ObservableCollection<LitSearchNodeViewModel> Children { get; } = new();

        public LitSearchFolderViewModel? Parent { get; set; }

        public bool CanDelete => !IsRoot;

        partial void OnNameInternalChanged(string value)
        {
            OnPropertyChanged(nameof(Name));
        }
    }
}
