using System;
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

        public string Title => "Save Library Preset";

        public string ResultName { get; private set; } = string.Empty;

        public void Initialize(LibraryPresetSaveContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            PresetName = context.DefaultName ?? string.Empty;
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(PresetName))
            {
                System.Windows.MessageBox.Show("Please provide a name for the preset.",
                                               "Save Library Preset",
                                               System.Windows.MessageBoxButton.OK,
                                               System.Windows.MessageBoxImage.Information);
                return;
            }

            ResultName = PresetName.Trim();
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
