#nullable enable
using System;
using System.ComponentModel;
using LM.App.Wpf.ViewModels.Library;

namespace LM.App.Wpf.Views.Library
{
    internal partial class DataExtractionPlaygroundWindow : System.Windows.Window
    {
        private readonly DataExtractionPlaygroundViewModel _viewModel;

        internal DataExtractionPlaygroundWindow(DataExtractionPlaygroundViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            base.OnClosed(e);
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            NavigateToPdf();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(DataExtractionPlaygroundViewModel.PdfSource), StringComparison.Ordinal))
            {
                NavigateToPdf();
            }
        }

        private void NavigateToPdf()
        {
            var source = _viewModel.PdfSource;
            if (source is null)
                return;

            try
            {
                PdfBrowser.Navigate(source);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load PDF preview:\n{ex.Message}",
                    "PDF preview",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
