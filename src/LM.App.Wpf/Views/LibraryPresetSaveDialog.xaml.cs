#nullable enable
using System;
using System.Windows;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;

namespace LM.App.Wpf.Views
{
    public partial class LibraryPresetSaveDialog : Window
    {
        private readonly LibraryPresetSaveDialogViewModel _viewModel;

        public LibraryPresetSaveDialog(LibraryPresetSaveDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
        }

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
