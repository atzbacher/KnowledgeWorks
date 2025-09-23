#nullable enable
using LM.App.Wpf.Common;
using LM.App.Wpf.Models;
using LM.App.Wpf.Services;
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
    public sealed class WatchedFoldersViewModel : INotifyPropertyChanged
    {
        private readonly IWatchedFolderConfigStore _configStore;
        private readonly IWatchedFolderScanner _scanner;
        private readonly IDialogService _dialogs;
        private readonly StagingListViewModel _staging;

        private readonly RelayCommand _addFolderCommand;
        private readonly RelayCommand _removeFolderCommand;
        private readonly RelayCommand _scanCommand;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<WatchedFolderItemViewModel> Folders { get; } = new();

        private WatchedFolderItemViewModel? _selectedFolder;
        public WatchedFolderItemViewModel? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (!ReferenceEquals(_selectedFolder, value))
                {
                    _selectedFolder = value;
                    OnPropertyChanged();
                    _removeFolderCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                _addFolderCommand.RaiseCanExecuteChanged();
                _removeFolderCommand.RaiseCanExecuteChanged();
                _scanCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand AddFolderCommand => _addFolderCommand;
        public ICommand RemoveFolderCommand => _removeFolderCommand;
        public ICommand ScanCommand => _scanCommand;

        public WatchedFoldersViewModel(IWatchedFolderConfigStore configStore,
                                       IWatchedFolderScanner scanner,
                                       IDialogService dialogs,
                                       StagingListViewModel staging)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _staging = staging ?? throw new ArgumentNullException(nameof(staging));

            _addFolderCommand = new RelayCommand(_ => AddFolder(), _ => !IsBusy);
            _removeFolderCommand = new RelayCommand(_ => RemoveSelected(), _ => !IsBusy && SelectedFolder is not null);
            _scanCommand = new RelayCommand(async _ => await RunGuardedAsync(ScanAsync), _ => !IsBusy && HasEnabledFolders());

            Folders.CollectionChanged += OnFoldersChanged;

            foreach (var folder in SafeLoad())
            {
                var vm = new WatchedFolderItemViewModel(folder);
                Attach(vm);
                Folders.Add(vm);
            }

            SelectedFolder = Folders.FirstOrDefault();
        }

        private IReadOnlyList<WatchedFolder> SafeLoad()
        {
            try
            {
                return _configStore.Load();
            }
            catch
            {
                return Array.Empty<WatchedFolder>();
            }
        }

        private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (WatchedFolderItemViewModel item in e.NewItems)
                    Attach(item);
            }

            if (e.OldItems != null)
            {
                foreach (WatchedFolderItemViewModel item in e.OldItems)
                    Detach(item);
            }

            _scanCommand.RaiseCanExecuteChanged();
            Persist();
        }

        private void Attach(WatchedFolderItemViewModel item)
        {
            item.PropertyChanged += OnFolderPropertyChanged;
        }

        private void Detach(WatchedFolderItemViewModel item)
        {
            item.PropertyChanged -= OnFolderPropertyChanged;
        }

        private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WatchedFolderItemViewModel.IsEnabled))
                _scanCommand.RaiseCanExecuteChanged();

            Persist();
        }

        private void AddFolder()
        {
            var path = _dialogs.ShowFolderBrowserDialog("Select a folder to watch");
            if (string.IsNullOrWhiteSpace(path)) return;

            if (Folders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedFolder = Folders.First(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
                return;
            }

            var vm = new WatchedFolderItemViewModel(path, includeSubdirectories: true, isEnabled: true);
            Folders.Add(vm);
            SelectedFolder = vm;
        }

        private void RemoveSelected()
        {
            if (SelectedFolder is null) return;

            var index = Folders.IndexOf(SelectedFolder);
            var removed = SelectedFolder;
            Folders.Remove(removed);

            if (Folders.Count == 0)
                SelectedFolder = null;
            else
            {
                var newIndex = Math.Clamp(index - 1, 0, Folders.Count - 1);
                SelectedFolder = Folders[newIndex];
            }

            Persist();
        }

        private bool HasEnabledFolders() => Folders.Any(f => f.IsEnabled);

        private async Task ScanAsync()
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in Folders.Where(f => f.IsEnabled))
            {
                var result = await _scanner.ScanAsync(folder.ToModel(), CancellationToken.None);
                foreach (var path in result)
                    uniquePaths.Add(path);
            }

            if (uniquePaths.Count > 0)
                await _staging.StagePathsAsync(uniquePaths, CancellationToken.None);
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await action(); }
            finally { IsBusy = false; }
        }

        private void Persist()
        {
            try
            {
                _configStore.Save(Folders.Select(f => f.ToModel()));
            }
            catch
            {
                // swallow persistence issues to keep UI responsive
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

