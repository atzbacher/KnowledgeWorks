using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Views
{
    public sealed class LibraryPresetPrompt : ILibraryPresetPrompt
    {
        private readonly IServiceProvider _services;

        public LibraryPresetPrompt(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return InvokeOnDispatcherAsync(() =>
            {
                using var scope = _services.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<LibraryPresetSaveDialogViewModel>();
                viewModel.Initialize(context);
                var dialog = scope.ServiceProvider.GetRequiredService<LibraryPresetSaveDialog>();
                SetOwner(dialog);

                var ok = dialog.ShowDialog();
                return ok == true ? new LibraryPresetSaveResult(viewModel.ResultName) : null;
            });
        }

        public Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (context.Presets.Count == 0)
                return Task.FromResult<LibraryPresetSelectionResult?>(null);

            return InvokeOnDispatcherAsync(() =>
            {
                using var scope = _services.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<LibraryPresetPickerDialogViewModel>();
                viewModel.Initialize(context);
                var dialog = scope.ServiceProvider.GetRequiredService<LibraryPresetPickerDialog>();
                SetOwner(dialog);

                dialog.ShowDialog();

                if (viewModel.DeletedPresetNames.Count == 0 && string.IsNullOrEmpty(viewModel.SelectedPresetName))
                    return null;

                return new LibraryPresetSelectionResult(
                    viewModel.SelectedPresetName,
                    viewModel.DeletedPresetNames.ToList());
            });
        }

        private static void SetOwner(Window dialog)
        {
            if (Application.Current?.MainWindow is Window owner && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
        }

        private static Task<TResult?> InvokeOnDispatcherAsync<TResult>(Func<TResult?> callback)
        {
            var app = Application.Current;
            if (app is null)
                return Task.FromResult<TResult?>(default);

            if (app.Dispatcher.CheckAccess())
                return Task.FromResult(callback());

            return app.Dispatcher.InvokeAsync(callback).Task;
        }
    }
}
