using System;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs.Projects;

namespace LM.App.Wpf.Views.Dialogs.Projects
{
    internal partial class ProjectCreationWindow : System.Windows.Window
    {
        private readonly ProjectCreationViewModel _viewModel;

        public ProjectCreationWindow(ProjectCreationViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
            base.OnClosed(e);
        }

        private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
        {
            DialogResult = e.DialogResult;
        }
    }
}
