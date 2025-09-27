namespace LM.App.Wpf.Views;

using System;
using System.Windows;
using LM.App.Wpf.ViewModels.Review;

public partial class ReviewWorkflowWindow : Window
{
    public ReviewWorkflowWindow(ReviewWorkflowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.Completed += OnCompleted;
        viewModel.Canceled += OnCanceled;
        Closed += (_, _) =>
        {
            viewModel.Completed -= OnCompleted;
            viewModel.Canceled -= OnCanceled;
        };
    }

    private void OnCompleted(object? sender, ReviewWorkflowCompletedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCanceled(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
