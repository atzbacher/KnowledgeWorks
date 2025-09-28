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
        private string tessTrainingDataPath = string.Empty;

        [ObservableProperty]
        private string title = "Choose workspace";

        public WorkspaceChooserViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public string? SelectedWorkspacePath { get; private set; }

        public string? SelectedTessTrainingDataPath { get; private set; }

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
        private void BrowseTrainingData()
        {
            var files = _dialogService.ShowOpenFileDialog(new FilePickerOptions
            {
                AllowMultiple = false,
                Filter = "Tesseract training (*.traineddata)|*.traineddata|All files (*.*)|*.*"
            });

            if (files is { Length: > 0 })
            {
                TessTrainingDataPath = files[0]!;
            }
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

            string? trainingDestination = null;
            var workspaceRoot = Path.GetFullPath(path);

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

            if (!string.IsNullOrWhiteSpace(TessTrainingDataPath))
            {
                var trainingPath = TessTrainingDataPath.Trim();
                if (!File.Exists(trainingPath))
                {
                    System.Windows.MessageBox.Show("Please choose an existing Tesseract training file.",
                                                   "Workspace",
                                                   System.Windows.MessageBoxButton.OK,
                                                   System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var targetDirectory = Path.Combine(workspaceRoot, ".knowledgeworks", "tessdata");
                    Directory.CreateDirectory(targetDirectory);

                    var sourceFullPath = Path.GetFullPath(trainingPath);
                    var targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourceFullPath));

                    if (!string.Equals(sourceFullPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourceFullPath, targetPath, overwrite: true);
                    }

                    trainingDestination = targetPath;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Unable to import the training data file:{Environment.NewLine}{ex.Message}",
                                                   "Workspace",
                                                   System.Windows.MessageBoxButton.OK,
                                                   System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            SelectedWorkspacePath = workspaceRoot;
            SelectedTessTrainingDataPath = trainingDestination;
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
