#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Services.Review;

namespace LM.App.Wpf.ViewModels.Dialogs
{
    public sealed partial class LitSearchRunPickerViewModel : DialogViewModelBase
    {
        private readonly ObservableCollection<LitSearchRunEntryItemViewModel> _entries = new();

        public LitSearchRunPickerViewModel()
        {
            Entries = new ReadOnlyObservableCollection<LitSearchRunEntryItemViewModel>(_entries);
        }

        public ReadOnlyObservableCollection<LitSearchRunEntryItemViewModel> Entries { get; }

        [ObservableProperty]
        private LitSearchRunEntryItemViewModel? selectedEntry;

        [ObservableProperty]
        private LitSearchRunItemViewModel? selectedRun;

        [ObservableProperty]
        private bool hasEntries;

        internal void Initialize(IReadOnlyList<LitSearchRunOption> options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _entries.Clear();
            foreach (var option in options)
            {
                _entries.Add(new LitSearchRunEntryItemViewModel(option));
            }

            HasEntries = _entries.Count > 0;
            SelectedEntry = _entries.FirstOrDefault();
            SelectedRun = SelectedEntry?.Runs.FirstOrDefault();

            ConfirmCommand.NotifyCanExecuteChanged();
        }

        internal LitSearchRunSelection? BuildSelection()
        {
            if (SelectedEntry is null || SelectedRun is null)
            {
                return null;
            }

            return new LitSearchRunSelection(
                SelectedEntry.EntryId,
                SelectedEntry.HookAbsolutePath,
                SelectedEntry.HookRelativePath,
                SelectedRun.RunId);
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!CanConfirm())
            {
                return;
            }

            RequestClose(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }

        private bool CanConfirm() => SelectedEntry is not null && SelectedRun is not null;

        partial void OnSelectedEntryChanged(LitSearchRunEntryItemViewModel? value)
        {
            if (value is null)
            {
                SelectedRun = null;
            }
            else if (!ReferenceEquals(value.Runs, SelectedRun?.Owner?.Runs))
            {
                SelectedRun = value.Runs.FirstOrDefault();
            }

            ConfirmCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedRunChanged(LitSearchRunItemViewModel? value)
        {
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }
}
