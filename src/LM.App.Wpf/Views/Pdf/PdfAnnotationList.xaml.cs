using System.Threading;
using LM.App.Wpf.ViewModels.Pdf;

namespace LM.App.Wpf.Views.Pdf
{
    public partial class PdfAnnotationList : System.Windows.Controls.UserControl
    {
        public PdfAnnotationList()
        {
            InitializeComponent();
        }

        private async void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is not PdfViewerViewModel viewModel)
            {
                return;
            }

            var listView = sender as System.Windows.Controls.ListView;
            var annotation = listView?.SelectedItem as PdfAnnotation;
            await viewModel.HandleAnnotationSelectionAsync(annotation, CancellationToken.None).ConfigureAwait(true);
        }
    }
}
