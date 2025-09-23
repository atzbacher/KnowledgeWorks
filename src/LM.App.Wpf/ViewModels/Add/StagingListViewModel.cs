#nullable enable
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.Core.Models;
using LM.App.Wpf.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LM.App.Wpf.ViewModels.Add
{
    public sealed class StagingListViewModel : INotifyPropertyChanged
    {
        private readonly IAddPipeline _pipeline;
        private readonly IDialogService _dialogs;

        private readonly RelayCommand _addFilesCommand;
        private readonly RelayCommand _bulkAddFolderCommand;
        private readonly RelayCommand _commitSelectedCommand;
        private readonly RelayCommand _clearCommand;
        private readonly RelayCommand _reviewCommand;

        private static readonly Array s_entryTypes = Enum.GetValues(typeof(EntryType));

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<StagingItem> Items { get; } = new();

        public Array EntryTypes => s_entryTypes;

        private StagingItem? _current;
        public StagingItem? Current
        {
            get => _current;
            set
            {
                if (!ReferenceEquals(_current, value))
                {
                    _current = value;
                    if (_current is not null)
                        _selectedType = _current.Type;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IndexLabel));
                    OnPropertyChanged(nameof(SelectedType));
                }
            }
        }

        private EntryType _selectedType;
        public EntryType SelectedType
        {
            get => _selectedType;
            set
            {
                if (_selectedType != value)
                {
                    _selectedType = value;
                    if (Current is not null)
                        Current.Type = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IndexLabel => Items.Count == 0 || Current is null
            ? "0 / 0"
            : $"{Items.IndexOf(Current) + 1} / {Items.Count}";

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();

                _addFilesCommand.RaiseCanExecuteChanged();
                _bulkAddFolderCommand.RaiseCanExecuteChanged();
                _commitSelectedCommand.RaiseCanExecuteChanged();
                _clearCommand.RaiseCanExecuteChanged();
                _reviewCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand AddFilesCommand => _addFilesCommand;
        public ICommand BulkAddFolderCommand => _bulkAddFolderCommand;
        public ICommand CommitSelectedCommand => _commitSelectedCommand;
        public ICommand ClearCommand => _clearCommand;
        public ICommand ReviewCommand => _reviewCommand;

        public StagingListViewModel(IAddPipeline pipeline, IDialogService dialogs)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

            Items.CollectionChanged += OnItemsChanged;

            _addFilesCommand = new RelayCommand(async _ => await RunGuardedAsync(AddFilesAsync), _ => !IsBusy);
            _bulkAddFolderCommand = new RelayCommand(async _ => await RunGuardedAsync(BulkAddFolderAsync), _ => !IsBusy);
            _commitSelectedCommand = new RelayCommand(async _ => await RunGuardedAsync(CommitSelectedAsync), CanCommitSelected);
            _clearCommand = new RelayCommand(_ => ExecuteClear(), _ => !IsBusy);
            _reviewCommand = new RelayCommand(_ => _dialogs.ShowStagingEditor(this), _ => !IsBusy && Items.Count > 0);
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (StagingItem item in e.NewItems)
                    item.PropertyChanged += OnStagingItemChanged;
            }

            if (e.OldItems != null)
            {
                foreach (StagingItem item in e.OldItems)
                    item.PropertyChanged -= OnStagingItemChanged;
            }

            _commitSelectedCommand.RaiseCanExecuteChanged();
            _reviewCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IndexLabel));
        }

        private void OnStagingItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StagingItem.Selected))
                _commitSelectedCommand.RaiseCanExecuteChanged();
        }

        public void SelectByOffset(int delta)
        {
            if (Items.Count == 0)
            {
                Current = null;
                return;
            }

            var currentIndex = Current is null ? 0 : Items.IndexOf(Current);
            var newIndex = Math.Clamp(currentIndex + delta, 0, Items.Count - 1);
            Current = Items[newIndex];
        }

        public Task StagePathsAsync(IEnumerable<string> paths, CancellationToken ct)
            => RunGuardedAsync(() => StageInternalAsync(paths, ct));

        private bool CanCommitSelected(object? _) => !IsBusy && Items.Any(i => i.Selected);

        private void ExecuteClear()
        {
            if (IsBusy) return;
            Items.Clear();
            Current = null;
            OnPropertyChanged(nameof(IndexLabel));
        }

        private async Task AddFilesAsync()
        {
            const string filter = "All supported|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.txt;*.md|All files|*.*";
            var files = _dialogs.ShowOpenFileDialog(filter, allowMultiple: true);
            if (files.Count == 0) return;

            await StageInternalAsync(files, CancellationToken.None);
        }

        private async Task BulkAddFolderAsync()
        {
            var folder = _dialogs.ShowFolderBrowserDialog("Select a folder to bulk add");
            if (string.IsNullOrWhiteSpace(folder)) return;

            var files = System.IO.Directory.EnumerateFiles(folder, "*.*", System.IO.SearchOption.AllDirectories);
            await StageInternalAsync(files, CancellationToken.None);
        }

        private async Task StageInternalAsync(IEnumerable<string> paths, CancellationToken ct)
        {
            var items = await _pipeline.StagePathsAsync(paths, ct);

            foreach (var item in items)
                Items.Add(item);

            Current ??= Items.FirstOrDefault();
            OnPropertyChanged(nameof(IndexLabel));
            _commitSelectedCommand.RaiseCanExecuteChanged();
            _reviewCommand.RaiseCanExecuteChanged();
        }

        private async Task CommitSelectedAsync()
        {
            var selected = Items.Where(i => i.Selected).ToList();
            if (selected.Count == 0) return;

            var committed = await _pipeline.CommitAsync(selected, CancellationToken.None);

            foreach (var item in committed)
                Items.Remove(item);

            Current = Items.FirstOrDefault();
            OnPropertyChanged(nameof(IndexLabel));
            _commitSelectedCommand.RaiseCanExecuteChanged();
            _reviewCommand.RaiseCanExecuteChanged();
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await action(); }
            finally { IsBusy = false; }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

