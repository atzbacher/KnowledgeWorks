using CommunityToolkit.Mvvm.ComponentModel;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.ViewModels.Library.LitSearch
{
    public abstract partial class LitSearchNodeViewModel : ObservableObject
    {
        protected LitSearchNodeViewModel(LitSearchTreeViewModel tree)
        {
            Tree = tree;
        }

        public LitSearchTreeViewModel Tree { get; }

        public abstract string Id { get; }

        public abstract string Name { get; }

        public abstract bool IsDraggable { get; }

        public LibraryNavigationNodeViewModel? NavigationNode { get; protected set; }
    }
}
