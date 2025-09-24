#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.Common;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    public sealed class WatchedFoldersViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly StagingListViewModel _stagingList;
        private readonly WatchedFolderScanner _scanner;
        private readonly IWatchedFolderSettingsStore _settingsStore;
        private readonly IDialogService _dialogService;
        private readonly SemaphoreSlim _initGate = new(1, 1);
        private readonly SemaphoreSlim _saveGate = new(1, 1);
        private readonly RelayCommand _addCommand;
        private readonly RelayCommand _removeCommand;
        private readonly RelayCommand _scanCommand;
        private readonly RelayCommand _scanAllCommand;
        private WatchedFolderConfig _config = new();
        private bool _disposed;
        private bool _isInitialized;
        private bool _isRestoring;
        private bool _isParentBusy;
        private Func<Func<Task>, Task> _commandGuard = static action => action();

        public event PropertyChangedEventHandler? PropertyChanged;

        public WatchedFoldersViewModel(StagingListViewModel stagingList,
                                       WatchedFolderScanner scanner,
                                       IWatchedFolderSettingsStore settingsStore,
                                       IDialogService dialogService)
        {
            _stagingList = stagingList ?? throw new ArgumentNullException(nameof(stagingList));
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            _addCommand = new RelayCommand(async _ => await ExecuteGuardedAsync(AddWatchedFolderCoreAsync), _ => CanExecuteGeneral());
            _removeCommand = new RelayCommand(RemoveWatchedFolder, CanExecuteFolderCommand);
            _scanCommand = new RelayCommand(async p => await ExecuteGuardedAsync(() => ScanWatchedFolderCoreAsync(p as WatchedFolder)), CanExecuteFolderCommand);
            _scanAllCommand = new RelayCommand(async _ => await ExecuteGuardedAsync(ScanAllWatchedFoldersCoreAsync), _ => CanExecuteScanAll());

            _scanner.ItemsStaged += OnScannerItemsStaged;
        }

        public ObservableCollection<WatchedFolder> Folders => _config.Folders;

        public System.Windows.Input.ICommand AddWatchedFolderCommand => _addCommand;
        public System.Windows.Input.ICommand RemoveWatchedFolderCommand => _removeCommand;
        public System.Windows.Input.ICommand ScanWatchedFolderCommand => _scanCommand;
        public System.Windows.Input.ICommand ScanAllWatchedFoldersCommand => _scanAllCommand;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized)
                return;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isInitialized)
                    return;

                WatchedFolderSettings settings;
                try
                {
                    settings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[WatchedFoldersViewModel] Failed to load watched folder config: {ex}");
                    settings = new WatchedFolderSettings();
                }

                var config = new WatchedFolderConfig();
                try
                {
                    config.Load(settings);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[WatchedFoldersViewModel] Failed to hydrate watched folder config: {ex}");
                }

                _config = config;
                OnPropertyChanged(nameof(Folders));

                _isRestoring = true;
                try
                {
                    Folders.CollectionChanged += OnFoldersChanged;
                    foreach (var folder in Folders)
                        folder.PropertyChanged += OnFolderPropertyChanged;
                }
                finally
                {
                    _isRestoring = false;
                }

                try
                {
                    _scanner.Attach(_config);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[WatchedFoldersViewModel] Failed to attach watched folder scanner: {ex}");
                }

                RefreshCommandStates();
                _isInitialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        public void SetCommandGuard(Func<Func<Task>, Task> guard)
        {
            _commandGuard = guard ?? throw new ArgumentNullException(nameof(guard));
        }

        public void UpdateParentBusy(bool isBusy)
        {
            if (_isParentBusy == isBusy)
                return;

            _isParentBusy = isBusy;
            RefreshCommandStates();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _scanner.ItemsStaged -= OnScannerItemsStaged;

            if (_isInitialized)
            {
                Folders.CollectionChanged -= OnFoldersChanged;
                foreach (var folder in Folders)
                    folder.PropertyChanged -= OnFolderPropertyChanged;
            }

            _scanner.Dispose();
        }

        private async Task ExecuteGuardedAsync(Func<Task> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            await _commandGuard(async () =>
            {
                await EnsureInitializedAsync().ConfigureAwait(false);
                await action().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private bool CanExecuteGeneral() => !_isParentBusy;

        private bool CanExecuteFolderCommand(object? parameter)
            => !_isParentBusy && parameter is WatchedFolder;

        private bool CanExecuteScanAll()
            => !_isParentBusy && Folders.Any(static f => f.IsEnabled);

        private async Task AddWatchedFolderCoreAsync()
        {
            var selected = _dialogService.ShowFolderBrowserDialog(new FolderPickerOptions
            {
                Description = "Select a folder to watch for new files"
            });

            if (string.IsNullOrWhiteSpace(selected))
                return;

            if (Folders.Any(f => string.Equals(f.Path, selected, StringComparison.OrdinalIgnoreCase)))
                return;

            var folder = new WatchedFolder
            {
                Path = selected.Trim(),
                IsEnabled = true
            };

            Folders.Add(folder);

            try
            {
                await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFoldersViewModel] Failed to scan '{folder.Path}': {ex}");
            }
        }

        private void RemoveWatchedFolder(object? parameter)
        {
            if (parameter is not WatchedFolder folder)
                return;

            Folders.Remove(folder);
        }

        private async Task ScanWatchedFolderCoreAsync(WatchedFolder? folder)
        {
            if (folder is null)
                return;

            try
            {
                await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFoldersViewModel] Failed to scan '{folder.Path}': {ex}");
            }
        }

        private async Task ScanAllWatchedFoldersCoreAsync()
        {
            if (Folders.Count == 0)
                return;

            var targets = Folders.Where(static f => f.IsEnabled).ToList();
            if (targets.Count == 0)
                return;

            foreach (var folder in targets)
            {
                try
                {
                    await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[WatchedFoldersViewModel] Failed to scan '{folder.Path}': {ex}");
                }
            }
        }

        private Task EnsureInitializedAsync()
            => _isInitialized ? Task.CompletedTask : InitializeAsync();

        private async void OnScannerItemsStaged(object? sender, WatchedFolderScanEventArgs e)
        {
            if (e?.Items is null || e.Items.Count == 0)
                return;

            try
            {
                await _stagingList.AddStagedItemsAsync(e.Items).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFoldersViewModel] Failed to append watched items: {ex}");
            }
        }

        private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (WatchedFolder folder in e.NewItems)
                {
                    folder.PropertyChanged += OnFolderPropertyChanged;
                    _config.ApplyState(folder);
                }
            }

            if (e.OldItems is not null)
            {
                foreach (WatchedFolder folder in e.OldItems)
                {
                    folder.PropertyChanged -= OnFolderPropertyChanged;
                    _config.ClearState(folder);
                }
            }

            ScheduleSave();
            RefreshCommandStates();
        }

        private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WatchedFolder folder)
                return;

            if (e.PropertyName == nameof(WatchedFolder.Path))
            {
                _config.ClearState(folder);
                ScheduleSave();
            }
            else if (e.PropertyName == nameof(WatchedFolder.IsEnabled))
            {
                ScheduleSave();
                RefreshCommandStates();
            }
            else if (e.PropertyName == nameof(WatchedFolder.LastScanUtc) ||
                     e.PropertyName == nameof(WatchedFolder.LastScanWasUnchanged))
            {
                ScheduleSave();
            }
        }

        private void ScheduleSave()
        {
            if (!_isInitialized || _isRestoring || _disposed)
                return;

            _ = SaveAsync(CancellationToken.None);
        }

        private async Task SaveAsync(CancellationToken ct)
        {
            await _saveGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshot = _config.CreateSnapshot();
                await _settingsStore.SaveAsync(snapshot, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFoldersViewModel] Failed to save watched folders: {ex}");
            }
            finally
            {
                _saveGate.Release();
            }
        }

        private void RefreshCommandStates()
        {
            _addCommand.RaiseCanExecuteChanged();
            _removeCommand.RaiseCanExecuteChanged();
            _scanCommand.RaiseCanExecuteChanged();
            _scanAllCommand.RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
