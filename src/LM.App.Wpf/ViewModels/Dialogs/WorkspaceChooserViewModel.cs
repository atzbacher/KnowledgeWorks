using System;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Diagnostics;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class WorkspaceChooserViewModel : DialogViewModelBase
    {
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string workspacePath = string.Empty;

        [ObservableProperty]
        private bool enableDebugDump;

        public WorkspaceChooserViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public string? SelectedWorkspacePath { get; private set; }

        public string Title => "Choose workspace";

        [RelayCommand]
        private void Browse()
        {
            var path = _dialogService.ShowFolderBrowserDialog(new FolderPickerOptions
            {
                Description = "Select the workspace folder (shared via OneDrive/SharePoint)"
            });

            if (!string.IsNullOrWhiteSpace(path))
                WorkspacePath = path!;
        }

        [RelayCommand]
        private void Confirm()
        {
            var path = WorkspacePath?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Please choose an existing folder.",
                                "Workspace",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            SelectedWorkspacePath = Path.GetFullPath(path);
            DebugFlags.DumpStagingJson = EnableDebugDump;
            RequestClose(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }
    }
}
