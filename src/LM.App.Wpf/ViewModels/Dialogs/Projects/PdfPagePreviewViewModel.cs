using CommunityToolkit.Mvvm.ComponentModel;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    internal sealed partial class PdfPagePreviewViewModel : ObservableObject
    {
        public PdfPagePreviewViewModel(PdfPagePreviewDefinition definition)
        {
            Definition = definition;
            PageNumber = definition.PageNumber;
            Caption = definition.Caption;
            Summary = definition.Summary;
        }

        public PdfPagePreviewDefinition Definition { get; }

        [ObservableProperty]
        private int pageNumber;

        [ObservableProperty]
        private string caption;

        [ObservableProperty]
        private string summary;
    }
}
