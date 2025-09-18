#nullable enable
using System.Threading.Tasks;
using System.Windows;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.Views
{
    public sealed class SearchSavePrompt : ISearchSavePrompt
    {
        public Task<SearchSavePromptResult?> RequestAsync(SearchSavePromptContext context)
        {
            var app = System.Windows.Application.Current;
            if (app is null)
            {
                return Task.FromResult<SearchSavePromptResult?>(null);
            }

            if (app.Dispatcher.CheckAccess())
            {
                return Task.FromResult(ShowDialog(context));
            }

            return app.Dispatcher.InvokeAsync(() => ShowDialog(context)).Task;
        }

        private static SearchSavePromptResult? ShowDialog(SearchSavePromptContext context)
        {
            var dialog = new SearchSaveDialog(context);
            if (System.Windows.Application.Current?.MainWindow is Window owner && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            var ok = dialog.ShowDialog();
            if (ok == true)
            {
                return new SearchSavePromptResult(dialog.ResultName, dialog.ResultNotes, dialog.ResultTagsRaw);
            }

            return null;
        }
    }
}
