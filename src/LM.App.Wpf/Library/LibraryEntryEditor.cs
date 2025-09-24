#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    internal sealed class LibraryEntryEditor : ILibraryEntryEditor
    {
        private readonly IServiceProvider _services;

        public LibraryEntryEditor(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task<bool> EditEntryAsync(Entry entry)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                System.Windows.MessageBox.Show(
                    "Selected entry is missing an identifier.",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return false;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
                throw new InvalidOperationException("Application dispatcher is not available.");

            if (!dispatcher.CheckAccess())
            {
                var operation = dispatcher.InvokeAsync(() => EditEntryOnUiThreadAsync(entry));
                return await operation.Task.Unwrap().ConfigureAwait(false);
            }

            return await EditEntryOnUiThreadAsync(entry).ConfigureAwait(true);
        }

        private async Task<bool> EditEntryOnUiThreadAsync(Entry entry)
        {
            using var scope = _services.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<EntryEditorViewModel>();

            var loaded = await viewModel.LoadAsync(entry.Id!).ConfigureAwait(true);
            if (!loaded)
                return false;

            var window = new EntryEditorWindow(viewModel)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            var result = window.ShowDialog();
            return result == true && viewModel.WasSaved;
        }
    }
}
