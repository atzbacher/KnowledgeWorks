#nullable enable
using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;

namespace LM.App.Wpf.Views.Review
{
    public partial class LitSearchRunPickerWindow : System.Windows.Window
    {
        private readonly LitSearchRunPickerViewModel _viewModel;

        public LitSearchRunPickerWindow(LitSearchRunPickerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
        }

        public LitSearchRunPickerViewModel ViewModel => _viewModel;

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            base.OnClosed(e);
        }

        private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
        {
            DialogResult = e.DialogResult;
        }
    }
}
