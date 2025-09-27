#nullable enable
using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Playground;

namespace LM.App.Wpf.Views.Playground
{
    internal partial class TabulaSharpPlaygroundWindow : System.Windows.Window
    {
        private readonly TabulaSharpPlaygroundViewModel _viewModel;

        public TabulaSharpPlaygroundWindow(TabulaSharpPlaygroundViewModel viewModel)
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
