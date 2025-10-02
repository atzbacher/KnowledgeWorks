using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class LibraryPresetSaveDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string presetName = string.Empty;

        [ObservableProperty]
        private string title = "Save Library Preset";

        [ObservableProperty]
        private string prompt = "Name this filter preset.";

        public string ResultName { get; private set; } = string.Empty;

        private IReadOnlyCollection<string> existingNames = Array.Empty<string>();

        public void Initialize(LibraryPresetSaveContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            ResultName = string.Empty;
            PresetName = context.DefaultName ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(context.Title) ? "Save Library Preset" : context.Title;
            Prompt = string.IsNullOrWhiteSpace(context.Prompt) ? "Name this filter preset." : context.Prompt;
            existingNames = context.ExistingNames ?? Array.Empty<string>();
        }

        [RelayCommand]
        private void Save()
        {
            var trimmed = PresetName?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                System.Windows.MessageBox.Show("Please provide a name for the preset.",
                                               Title,
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Information);
                return;
            }

            if (existingNames.Any(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"A preset named '{trimmed}' already exists.",
                                               Title,
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Warning);
                return;
            }

            ResultName = trimmed;
            RequestClose(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            ResultName = string.Empty;
            RequestClose(false);
        }
    }
}
