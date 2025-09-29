using System;

namespace LM.App.Wpf.Views.Library
{
    internal partial class MuPdfPlaygroundWindow : System.Windows.Window
    {
        private readonly ViewModels.Library.MuPdfPlaygroundViewModel _viewModel;

        internal MuPdfPlaygroundWindow(ViewModels.Library.MuPdfPlaygroundViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.Dispose();
        }
    }
}
