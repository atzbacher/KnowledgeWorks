using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class LibraryPresetPickerDialogViewModel : DialogViewModelBase
    {
        private readonly List<string> _deleted = new();

        [ObservableProperty]
        private LibraryPresetSummary? selectedPreset;

        public ObservableCollection<LibraryPresetSummary> Presets { get; } = new();

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool allowLoad;

        public IReadOnlyList<string> DeletedPresetNames => _deleted;

        public string? SelectedPresetName { get; private set; }

        public void Initialize(LibraryPresetSelectionContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            Title = context.Title;
            AllowLoad = context.AllowLoad;
            _deleted.Clear();
            SelectedPresetName = null;

            Presets.Clear();
            foreach (var preset in context.Presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                Presets.Add(preset);
            }

            SelectedPreset = Presets.FirstOrDefault();

            LoadCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            if (SelectedPreset is null)
                return;

            var confirm = System.Windows.MessageBox.Show(
                $"Delete preset \"{SelectedPreset.Name}\"?",
                Title,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirm != System.Windows.MessageBoxResult.Yes)
                return;

            _deleted.Add(SelectedPreset.Name);
            var index = Presets.IndexOf(SelectedPreset);
            Presets.Remove(SelectedPreset);

            SelectedPreset = Presets.Count == 0
                ? null
                : Presets[Math.Min(index, Presets.Count - 1)];
        }

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private void Load()
        {
            if (!AllowLoad || SelectedPreset is null)
                return;

            SelectedPresetName = SelectedPreset.Name;
            RequestClose(true);
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose(false);
        }

        private bool CanDelete() => SelectedPreset is not null;

        private bool CanLoad() => AllowLoad && SelectedPreset is not null;

        partial void OnSelectedPresetChanged(LibraryPresetSummary? value)
        {
            LoadCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }
}
