namespace LM.App.Wpf.ViewModels.Library.LitSearch
{
    public sealed class LitSearchRunViewModel : LitSearchNodeViewModel
    {
        public LitSearchRunViewModel(LitSearchTreeViewModel tree, string runId, string label, LitSearchEntryViewModel parent)
            : base(tree)
        {
            RunId = runId;
            Label = label;
            Parent = parent;
        }

        public string RunId { get; }

        public string Label { get; }

        public LitSearchEntryViewModel Parent { get; }

        public override string Id => RunId;

        public override string Name => Label;

        public override bool IsDraggable => false;
    }
}
