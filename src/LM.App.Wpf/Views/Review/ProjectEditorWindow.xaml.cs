#nullable enable
using System;
using LM.App.Wpf.ViewModels.Review;

namespace LM.App.Wpf.Views.Review;

public partial class ProjectEditorWindow : System.Windows.Window
{
    private ProjectEditorViewModel? _viewModel;

    public ProjectEditorWindow()
    {
        InitializeComponent();
    }

    public void Attach(ProjectEditorViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, LM.App.Wpf.Common.Dialogs.DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
