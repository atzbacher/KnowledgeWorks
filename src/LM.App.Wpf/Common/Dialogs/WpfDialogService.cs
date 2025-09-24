#nullable enable
using System;
using System.Linq;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.Common.Dialogs
{
    public sealed class WpfDialogService : IDialogService
    {
        private readonly IServiceProvider _services;

        public WpfDialogService(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public string[]? ShowOpenFileDialog(FilePickerOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = string.IsNullOrWhiteSpace(options.Filter)
                    ? "All files|*.*"
                    : options.Filter,
                Multiselect = options.AllowMultiple
            };

            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        public string? ShowFolderBrowserDialog(FolderPickerOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = options.Description ?? string.Empty,
                UseDescriptionForTitle = !string.IsNullOrWhiteSpace(options.Description)
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dialog.SelectedPath
                : null;
        }

        public bool? ShowStagingEditor(StagingListViewModel stagingList)
        {
            if (stagingList is null)
                throw new ArgumentNullException(nameof(stagingList));

            using var scope = _services.CreateScope();
            var window = scope.ServiceProvider.GetRequiredService<StagingEditorWindow>();
            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(static w => w.IsActive);
            if (owner is not null)
                window.Owner = owner;

            return window.ShowDialog();
        }
    }
}
