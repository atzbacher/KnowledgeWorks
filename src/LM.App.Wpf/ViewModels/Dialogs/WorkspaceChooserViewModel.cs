using System;
using System.IO;
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

        [ObservableProperty]
        private string title = "Choose workspace";

        public WorkspaceChooserViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public string? SelectedWorkspacePath { get; private set; }

        public bool RequireExistingDirectory { get; set; } = true;

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
            if (string.IsNullOrWhiteSpace(path))
            {
                System.Windows.MessageBox.Show("Please choose a folder.",
                                               "Workspace",
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!RequireExistingDirectory && !Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                else if (RequireExistingDirectory && !Directory.Exists(path))
                {
                    System.Windows.MessageBox.Show("Please choose an existing folder.",
                                                   "Workspace",
                                                   System.Windows.MessageBoxButton.OK,
                                                   System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Unable to use the selected folder:{Environment.NewLine}{ex.Message}",
                                               "Workspace",
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Error);
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
