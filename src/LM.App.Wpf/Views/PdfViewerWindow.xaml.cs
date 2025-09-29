using System;
using LM.App.Wpf.ViewModels.Pdf;

namespace LM.App.Wpf.Views
{
    public partial class PdfViewerWindow : System.Windows.Window
    {
        public PdfViewerWindow()
        {
            InitializeComponent();
        }

        internal void Attach(PdfViewerViewModel viewModel)
        {
            if (viewModel is null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            DataContext = viewModel;
        }
    }
}
