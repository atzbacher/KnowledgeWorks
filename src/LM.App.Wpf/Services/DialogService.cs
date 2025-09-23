#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LM.App.Wpf.ViewModels.Add;
using LM.App.Wpf.Views;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Default WPF-backed dialog service.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        public IReadOnlyList<string> ShowOpenFileDialog(string filter, bool allowMultiple)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Multiselect = allowMultiple
            };

            return dlg.ShowDialog() == true
                ? dlg.FileNames
                : Array.Empty<string>();
        }

        public string? ShowFolderBrowserDialog(string description)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = description,
                ShowNewFolderButton = true
            };

            return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dlg.SelectedPath
                : null;
        }

        public void ShowStagingEditor(StagingListViewModel stagingViewModel)
        {
            if (stagingViewModel is null) throw new ArgumentNullException(nameof(stagingViewModel));

            var owner = Application.Current?.Windows
                              .OfType<Window>()
                              .FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow;

            var window = new StagingEditorWindow(stagingViewModel)
            {
                Owner = owner
            };

            window.ShowDialog();
        }
    }
}

