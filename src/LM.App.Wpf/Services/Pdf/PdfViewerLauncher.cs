using System;
using System.Linq;
using LM.App.Wpf.ViewModels.Pdf;
using LM.App.Wpf.Views;

namespace LM.App.Wpf.Services.Pdf
{
    internal sealed class PdfViewerLauncher : IPdfViewerLauncher
    {
        private readonly PdfViewerViewModel _viewModel;
        private readonly IServiceProvider _services;
        private PdfViewerWindow? _window;

        public PdfViewerLauncher(PdfViewerViewModel viewModel, IServiceProvider services)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void Show(string entryId, string pdfAbsolutePath, string pdfHash)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry identifier must be provided.", nameof(entryId));
            }

            if (string.IsNullOrWhiteSpace(pdfAbsolutePath))
            {
                throw new ArgumentException("PDF path must be provided.", nameof(pdfAbsolutePath));
            }

            if (string.IsNullOrWhiteSpace(pdfHash))
            {
                throw new ArgumentException("PDF hash must be provided.", nameof(pdfHash));
            }

            if (!WebView2RuntimeBootstrapper.TryEnsureRuntime(out var bootstrapError))
            {
                System.Windows.MessageBox.Show(
                    bootstrapError ?? "The WebView2 runtime required for the PDF viewer is missing.",
                    "PDF Viewer",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            var normalizedHash = pdfHash.Trim().ToLowerInvariant();

            EnsureWindow();

            _viewModel.InitializeContext(entryId.Trim(), pdfAbsolutePath.Trim(), normalizedHash);
            _viewModel.LoadPdfCommand.Execute(null);

            if (_window is null)
            {
                return;
            }

            if (!_window.IsVisible)
            {
                AttachOwner(_window);
                _window.Show();
            }
            else
            {
                if (_window.WindowState == System.Windows.WindowState.Minimized)
                {
                    _window.WindowState = System.Windows.WindowState.Normal;
                }

                _window.Activate();
            }
        }

        private void EnsureWindow()
        {
            if (_window is not null)
            {
                return;
            }

            if (_services.GetService(typeof(PdfViewerWindow)) is not PdfViewerWindow resolved)
            {
                throw new InvalidOperationException("PdfViewerWindow is not registered in the service provider.");
            }

            _window = resolved;
            _window.Attach(_viewModel);
            _window.Closed += OnWindowClosed;
        }

        private static void AttachOwner(PdfViewerWindow window)
        {
            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(static w => w.IsActive);

            if (owner is not null)
            {
                window.Owner = owner;
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (_window is not null)
            {
                _window.Closed -= OnWindowClosed;
            }

            _window = null;
        }
    }
}
