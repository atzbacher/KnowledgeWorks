#nullable enable
using System;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Views
{
    public sealed class SearchSavePrompt : ISearchSavePrompt
    {
        private readonly IServiceProvider _services;

        public SearchSavePrompt(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public Task<SearchSavePromptResult?> RequestAsync(SearchSavePromptContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return InvokeOnDispatcherAsync(() =>
            {
                using var scope = _services.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<SearchSaveDialogViewModel>();
                viewModel.Initialize(context);
                var dialog = scope.ServiceProvider.GetRequiredService<SearchSaveDialog>();
                SetOwner(dialog);

                var ok = dialog.ShowDialog();
                return ok == true
                    ? new SearchSavePromptResult(viewModel.ResultName, viewModel.ResultNotes, viewModel.ResultTags)
                    : null;
            });
        }

        private static void SetOwner(System.Windows.Window dialog)
        {
            if (System.Windows.Application.Current?.MainWindow is System.Windows.Window owner && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
        }

        private static Task<TResult?> InvokeOnDispatcherAsync<TResult>(Func<TResult?> callback)
        {
            var app = System.Windows.Application.Current;
            if (app is null)
                return Task.FromResult<TResult?>(default);

            if (app.Dispatcher.CheckAccess())
                return Task.FromResult(callback());

            return app.Dispatcher.InvokeAsync(callback).Task;
        }
    }
}
