#nullable enable
using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.Views
{
    internal partial class EntryEditorWindow : System.Windows.Window
    {
        private readonly EntryEditorViewModel _viewModel;

        public EntryEditorWindow(EntryEditorViewModel viewModel)
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
