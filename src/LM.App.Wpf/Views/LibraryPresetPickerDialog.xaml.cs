#nullable enable
using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;

namespace LM.App.Wpf.Views
{
    public partial class LibraryPresetPickerDialog : System.Windows.Window
    {
        private readonly LibraryPresetPickerDialogViewModel _viewModel;

        public LibraryPresetPickerDialog(LibraryPresetPickerDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
        }

        public LibraryPresetPickerDialogViewModel ViewModel => _viewModel;

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
