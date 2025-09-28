#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views.Library;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    internal sealed class LibraryDataExtractionLauncher
    {
        private readonly IServiceProvider _services;

        public LibraryDataExtractionLauncher(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public Task<bool> LaunchAsync(Entry entry, CancellationToken cancellationToken)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
                throw new InvalidOperationException("Application dispatcher is not available.");

            if (!dispatcher.CheckAccess())
            {
                var operation = dispatcher.InvokeAsync(() => LaunchInternalAsync(entry, cancellationToken));
                return operation.Task.Unwrap();
            }

            return LaunchInternalAsync(entry, cancellationToken);
        }

        private async Task<bool> LaunchInternalAsync(Entry entry, CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<DataExtractionPlaygroundViewModel>();

            var initialized = await viewModel.InitializeAsync(entry, cancellationToken).ConfigureAwait(true);
            if (!initialized)
            {
                return false;
            }

            var window = new DataExtractionPlaygroundWindow(viewModel)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            window.Show();
            return true;
        }
    }
}
