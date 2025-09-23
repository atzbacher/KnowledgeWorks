#nullable enable
using System;
using System.Linq;
using System.Windows;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.Views;
using WpfApplication = System.Windows.Application;

namespace LM.App.Wpf.Common.Dialogs
{
    public sealed class WpfDialogService : IDialogService
    {
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

            var window = new StagingEditorWindow(stagingList)
            {
                Owner = WpfApplication.Current?.Windows
                    .OfType<Window>()
                    .FirstOrDefault(static w => w.IsActive)
            };

            return window.ShowDialog();
        }
    }
}
