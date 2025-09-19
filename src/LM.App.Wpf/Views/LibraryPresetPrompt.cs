using System.Threading.Tasks;
using System.Windows;
using LM.App.Wpf.Common;

namespace LM.App.Wpf.Views
{
    public sealed class LibraryPresetPrompt : ILibraryPresetPrompt
    {
        public Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context)
        {
            var app = Application.Current;
            if (app is null)
                return Task.FromResult<LibraryPresetSaveResult?>(null);

            if (app.Dispatcher.CheckAccess())
            {
                return Task.FromResult(ShowSaveDialog(context));
            }

            return app.Dispatcher.InvokeAsync(() => ShowSaveDialog(context)).Task;
        }

        public Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context)
        {
            var app = Application.Current;
            if (app is null)
                return Task.FromResult<LibraryPresetSelectionResult?>(null);

            if (app.Dispatcher.CheckAccess())
            {
                return Task.FromResult(ShowSelectionDialog(context));
            }

            return app.Dispatcher.InvokeAsync(() => ShowSelectionDialog(context)).Task;
        }

        private static LibraryPresetSaveResult? ShowSaveDialog(LibraryPresetSaveContext context)
        {
            var dialog = new LibraryPresetSaveDialog(context);
            if (Application.Current?.MainWindow is Window owner && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            var ok = dialog.ShowDialog();
            return ok == true ? new LibraryPresetSaveResult(dialog.ResultName) : null;
        }

        private static LibraryPresetSelectionResult? ShowSelectionDialog(LibraryPresetSelectionContext context)
        {
            if (context.Presets.Count == 0)
                return null;

            var dialog = new LibraryPresetPickerDialog(context);
            if (Application.Current?.MainWindow is Window owner && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();
            var deleted = dialog.DeletedPresetNames;
            var selected = dialog.SelectedPresetName;

            if (deleted.Count == 0 && string.IsNullOrEmpty(selected))
                return null;

            return new LibraryPresetSelectionResult(selected, deleted);
        }
    }
}
