#nullable enable
using System;
using System.Linq;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Dialogs.Projects;
using LM.App.Wpf.ViewModels.Dialogs.Staging;
using LM.App.Wpf.Views;
using LM.App.Wpf.Views.Dialogs.Staging;
using LM.App.Wpf.Views.Dialogs.Projects;
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

        public string? ShowSaveFileDialog(FileSavePickerOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = string.IsNullOrWhiteSpace(options.Filter) ? "All files|*.*" : options.Filter,
                FileName = options.DefaultFileName ?? string.Empty,
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
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

        public bool? ShowDataExtractionWorkspace(StagingItem stagingItem)
        {
            if (stagingItem is null)
                throw new ArgumentNullException(nameof(stagingItem));

            using var scope = _services.CreateScope();
            var viewModel = ActivatorUtilities.CreateInstance<DataExtractionWorkspaceViewModel>(scope.ServiceProvider, stagingItem);
            var window = ActivatorUtilities.CreateInstance<DataExtractionWorkspaceWindow>(scope.ServiceProvider, viewModel);

            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(static w => w.IsActive);
            if (owner is not null)
                window.Owner = owner;

            return window.ShowDialog();
        }

        public bool? ShowProjectCreation(ProjectCreationRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            using var scope = _services.CreateScope();
            var viewModel = ActivatorUtilities.CreateInstance<ProjectCreationViewModel>(scope.ServiceProvider, request);
            var window = ActivatorUtilities.CreateInstance<ProjectCreationWindow>(scope.ServiceProvider, viewModel);

            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(static w => w.IsActive);
            if (owner is not null)
                window.Owner = owner;

            return window.ShowDialog();
        }
    }
}
