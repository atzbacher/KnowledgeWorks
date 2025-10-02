using System.Collections.ObjectModel;

namespace LM.App.Wpf.ViewModels.Library.LitSearch
{
    public sealed class LitSearchEntryViewModel : LitSearchNodeViewModel
    {
        public LitSearchEntryViewModel(LitSearchTreeViewModel tree, string id, string title, string? query)
            : base(tree)
        {
            Id = id;
            Title = title;
            Query = query;
        }

        public override string Id { get; }

        public override string Name => Title;

        public override bool IsDraggable => true;

        public string Title { get; }

        public string? Query { get; }

        public ObservableCollection<LitSearchRunViewModel> Runs { get; } = new();

        public LitSearchFolderViewModel? Parent { get; set; }
    }
}
