using System.Collections.ObjectModel;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed class ProjectDocumentViewModel
    {
        public ProjectDocumentViewModel(ScreeningDocumentDefinition definition)
        {
            Definition = definition;
            Title = definition.Title;
            AbstractText = definition.AbstractText;
            Authors = new ObservableCollection<string>(definition.Authors);
            Keywords = new ObservableCollection<string>(definition.Keywords);
            Attributes = new ObservableCollection<DocumentAttributeDefinition>(definition.Attributes);
        }

        public ScreeningDocumentDefinition Definition { get; }

        public string Title { get; }

        public string? AbstractText { get; }

        public ObservableCollection<string> Authors { get; }

        public ObservableCollection<string> Keywords { get; }

        public ObservableCollection<DocumentAttributeDefinition> Attributes { get; }
    }
}
