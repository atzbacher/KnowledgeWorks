#nullable enable
using LM.App.Wpf.Common;           // RelayCommand
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // NotifyCollectionChangedEventArgs
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LM.Infrastructure.Hooks;     // for HookOrchestrator I
using LM.Core.Abstractions;       // for IPmidNormalizerEntryStore, IFileStorageRepository, IHasher, ISimilarityService, IWorkSpaceService, IMetadataExtractor, IDoiNormalizer, IPublicationLookup
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;              // EntryType
using LM.Infrastructure.Settings;
using LM.HubSpoke.Abstractions;    // ISimilarityLog

namespace LM.App.Wpf.ViewModels
{
    public class AddViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly IAddPipeline _pipeline;
        private readonly IWorkSpaceService _workspace;
        private readonly WatchedFolderScanner _scanner;
        private readonly IWatchedFolderSettingsStore _watchedFolderSettings;
        private readonly bool _ownsScanner;
        private WatchedFolderConfig _watchedConfig = new();
        private readonly SemaphoreSlim _watchedFolderSaveGate = new(1, 1);
        private readonly SemaphoreSlim _initGate = new(1, 1);
        private bool _isRestoringWatchedFolders;
        private bool _isInitialized;
        private bool _disposed;

        // Cache RelayCommand references to avoid repeated casting
        private readonly RelayCommand _addFilesCommand;
        private readonly RelayCommand _bulkAddFolderCommand;
        private readonly RelayCommand _commitSelectedCommand;
        private readonly RelayCommand _clearCommand;
        private readonly RelayCommand _addWatchedFolderCommand;
        private readonly RelayCommand _removeWatchedFolderCommand;
        private readonly RelayCommand _scanWatchedFolderCommand;
        private readonly RelayCommand _scanAllWatchedFoldersCommand;

        // Cache EntryTypes array
        private static readonly Array s_entryTypes = Enum.GetValues(typeof(EntryType));

        // ---- Primary ctor (preferred) ----
        public AddViewModel(IAddPipeline pipeline,
                            IWorkSpaceService workspace,
                            WatchedFolderScanner? scanner = null,
                            IWatchedFolderSettingsStore? watchedFolderSettings = null)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _scanner = scanner ?? new WatchedFolderScanner(_pipeline);
            _watchedFolderSettings = watchedFolderSettings ?? new JsonWatchedFolderSettingsStore(_workspace);
            _ownsScanner = scanner is null;

            // Track collection changes AND per-item Selected changes -> keeps Commit button state correct
            Staging.CollectionChanged += OnStagingCollectionChanged;

            // Cache command instances
            _addFilesCommand = new RelayCommand(async _ => await RunGuardedAsync(AddFilesAsync), _ => !IsBusy);
            _bulkAddFolderCommand = new RelayCommand(async _ => await RunGuardedAsync(BulkAddFolderAsync), _ => !IsBusy);
            _commitSelectedCommand = new RelayCommand(async _ => await RunGuardedAsync(CommitSelectedAsync), CanCommitSelected);
            _clearCommand = new RelayCommand(ExecuteClear, _ => !IsBusy);

            _addWatchedFolderCommand = new RelayCommand(async _ => await RunGuardedAsync(AddWatchedFolderAsync), _ => !IsBusy);
            _removeWatchedFolderCommand = new RelayCommand(p => RemoveWatchedFolder(p as WatchedFolder), p => !IsBusy && p is WatchedFolder);
            _scanWatchedFolderCommand = new RelayCommand(async p => await RunGuardedAsync(() => ScanWatchedFolderAsync(p as WatchedFolder)), p => !IsBusy && p is WatchedFolder);
            _scanAllWatchedFoldersCommand = new RelayCommand(async _ => await RunGuardedAsync(ScanAllWatchedFoldersAsync), _ => !IsBusy && WatchedFolders.Any(static f => f.IsEnabled));

            _scanner.ItemsStaged += OnScannerItemsStaged;
        }

        // ---- Back-compat ctor (old 7-arg wiring still works) ----

        public AddViewModel(IEntryStore store,
                            IFileStorageRepository storage,
                            IHasher hasher,
                            ISimilarityService similarity,
                            IWorkSpaceService workspace,
                            IMetadataExtractor metadata,
                            IPublicationLookup publicationLookup,
                            IDoiNormalizer doiNormalizer,
                            HookOrchestrator orchestrator,
                            IPmidNormalizer pmidNormalizer,
                            ISimilarityLog? simLog = null)
            : this(new AddPipeline(store, storage, hasher, similarity, workspace, metadata,
                                   publicationLookup, doiNormalizer,
                                   orchestrator,
                                   pmidNormalizer,   // <-- here
                                   simLog),
                   workspace)
        { }

        // If you also expose the 10-arg overload explicitly (including orchestrator), keep it:
        public AddViewModel(IEntryStore store,
                            IFileStorageRepository storage,
                            IHasher hasher,
                            ISimilarityService similarity,
                            IWorkSpaceService workspace,
                            IMetadataExtractor metadata,
                            IPublicationLookup publicationLookup,
                            IDoiNormalizer doiNormalizer,
                            ISimilarityLog? simLog = null)
            : this(store, storage, hasher, similarity, workspace, metadata,
                   publicationLookup, doiNormalizer,
                   new HookOrchestrator(workspace),
                   new LM.Infrastructure.Text.PmidNormalizer(),
                   simLog)
        { }


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
                    settings = await _watchedFolderSettings.LoadAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddViewModel] Failed to load watched folder config: {ex}");
                    settings = new WatchedFolderSettings();
                }

                var config = new WatchedFolderConfig();
                try
                {
                    config.Load(settings);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddViewModel] Failed to hydrate watched folder config: {ex}");
                }

                _watchedConfig = config;
                OnPropertyChanged(nameof(WatchedFolders));
                _scanAllWatchedFoldersCommand.RaiseCanExecuteChanged();

                _isRestoringWatchedFolders = true;
                try
                {
                    WatchedFolders.CollectionChanged += OnWatchedFoldersChanged;
                    foreach (var folder in WatchedFolders)
                        folder.PropertyChanged += OnWatchedFolderPropertyChanged;
                }
                finally
                {
                    _isRestoringWatchedFolders = false;
                }

                try
                {
                    _scanner.Attach(_watchedConfig);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AddViewModel] Failed to attach watched folder scanner: {ex}");
                }

                _isInitialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        // ---------------- UI state ----------------

        public ObservableCollection<StagingItem> Staging { get; } = new();
        public ObservableCollection<WatchedFolder> WatchedFolders => _watchedConfig.Folders;
        public Array EntryTypes => s_entryTypes;

        public string IndexLabel => Staging.Count == 0 || Current is null
            ? "0 / 0"
            : $"{Staging.IndexOf(Current) + 1} / {Staging.Count}";

        private StagingItem? _current;
        public StagingItem? Current
        {
            get => _current;
            set
            {
                if (!ReferenceEquals(_current, value))
                {
                    _current = value;
                    if (_current is not null) _selectedType = _current.Type;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedType));
                    OnPropertyChanged(nameof(IndexLabel));
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
                    if (Current is not null) Current.Type = value;
                    OnPropertyChanged();
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

                _addFilesCommand.RaiseCanExecuteChanged();
                _bulkAddFolderCommand.RaiseCanExecuteChanged();
                _commitSelectedCommand.RaiseCanExecuteChanged();
                _clearCommand.RaiseCanExecuteChanged();
                _addWatchedFolderCommand.RaiseCanExecuteChanged();
                _removeWatchedFolderCommand.RaiseCanExecuteChanged();
                _scanWatchedFolderCommand.RaiseCanExecuteChanged();
                _scanAllWatchedFoldersCommand.RaiseCanExecuteChanged();
            }
        }

        // Commands
        public ICommand AddFilesCommand => _addFilesCommand;
        public ICommand BulkAddFolderCommand => _bulkAddFolderCommand;
        public ICommand CommitSelectedCommand => _commitSelectedCommand;
        public ICommand ClearCommand => _clearCommand;
        public ICommand AddWatchedFolderCommand => _addWatchedFolderCommand;
        public ICommand RemoveWatchedFolderCommand => _removeWatchedFolderCommand;
        public ICommand ScanWatchedFolderCommand => _scanWatchedFolderCommand;
        public ICommand ScanAllWatchedFoldersCommand => _scanAllWatchedFoldersCommand;

        private bool CanCommitSelected(object? _) => !IsBusy && HasSelectedItems();
        private bool HasSelectedItems() => Staging.Any(s => s.Selected);

        private void ExecuteClear(object? _)
        {
            if (!IsBusy)
            {
                Staging.Clear();
                Current = null;
            }
        }

        // ---------------- UI actions ----------------

        private async Task AddFilesAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All supported|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.txt;*.md|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            await AddItemsToStagingAsync(dlg.FileNames);
        }

        private async Task BulkAddFolderAsync()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var files = System.IO.Directory.EnumerateFiles(dlg.SelectedPath, "*.*", System.IO.SearchOption.AllDirectories);
            await AddItemsToStagingAsync(files);
        }

        private async Task AddItemsToStagingAsync(System.Collections.Generic.IEnumerable<string> paths)
        {
            var items = await _pipeline.StagePathsAsync(paths, CancellationToken.None).ConfigureAwait(false);
            await AddStagedItemsAsync(items).ConfigureAwait(false);
        }

        private Task AddStagedItemsAsync(System.Collections.Generic.IReadOnlyList<StagingItem> items)
        {
            if (items is null || items.Count == 0)
                return Task.CompletedTask;

            void Add()
            {
                foreach (var item in items)
                    Staging.Add(item);

                Current ??= Staging.FirstOrDefault();
                OnPropertyChanged(nameof(IndexLabel));
                _commitSelectedCommand.RaiseCanExecuteChanged();
            }


            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher is null)
            {
                Add();
                return Task.CompletedTask;
            }

            if (dispatcher.CheckAccess())
            {
                Add();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(Add).Task;
        }

        private async void OnScannerItemsStaged(object? sender, WatchedFolderScanEventArgs e)
        {
            if (e?.Items is null || e.Items.Count == 0)
                return;

            try
            {
                await AddStagedItemsAsync(e.Items).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddViewModel] Failed to append watched items: {ex}");
            }
        }

        private async Task CommitSelectedAsync()
        {
            var selectedItems = Staging.Where(r => r.Selected).ToList();
            if (selectedItems.Count == 0) return;

            var committed = await _pipeline.CommitAsync(selectedItems, CancellationToken.None);

            foreach (var committedItem in committed)
                Staging.Remove(committedItem);

            Current = Staging.FirstOrDefault();
            OnPropertyChanged(nameof(IndexLabel));
            _commitSelectedCommand.RaiseCanExecuteChanged();
        }

        public void SelectByOffset(int delta)
        {
            if (Staging.Count == 0)
            {
                Current = null;
                return;
            }

            var currentIndex = Current is null ? 0 : Staging.IndexOf(Current);
            var newIndex = Math.Clamp(currentIndex + delta, 0, Staging.Count - 1);
            Current = Staging[newIndex];
        }

        // ---- Watched folder helpers ----

        private Task EnsureInitializedAsync()
            => _isInitialized ? Task.CompletedTask : InitializeAsync();

        private async Task AddWatchedFolderAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder to watch for new files"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var selected = dialog.SelectedPath?.Trim();
            if (string.IsNullOrWhiteSpace(selected))
                return;

            if (WatchedFolders.Any(f => string.Equals(f.Path, selected, StringComparison.OrdinalIgnoreCase)))
                return;

            var folder = new WatchedFolder
            {
                Path = selected,
                IsEnabled = true
            };

            WatchedFolders.Add(folder);

            await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ScanWatchedFolderAsync(WatchedFolder? folder)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (folder is null)
                return;

            await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ScanAllWatchedFoldersAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (WatchedFolders.Count == 0)
                return;

            List<WatchedFolder> targets = WatchedFolders
                .Where(static f => f.IsEnabled)
                .ToList();

            if (targets.Count == 0)
                return;

            foreach (var folder in targets)
            {
                await _scanner.ScanAsync(folder, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private void RemoveWatchedFolder(WatchedFolder? folder)
        {
            if (!_isInitialized)
                return;

            if (folder is null)
                return;

            if (WatchedFolders.Contains(folder))
                WatchedFolders.Remove(folder);
        }

        private void OnWatchedFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (WatchedFolder folder in e.NewItems)
                {
                    folder.PropertyChanged += OnWatchedFolderPropertyChanged;
                    _watchedConfig.ApplyState(folder);
                }
            }

            if (e.OldItems is not null)
            {
                foreach (WatchedFolder folder in e.OldItems)
                {
                    folder.PropertyChanged -= OnWatchedFolderPropertyChanged;
                    _watchedConfig.ClearState(folder);
                }
            }

            ScheduleWatchedFolderSave();
            _scanAllWatchedFoldersCommand.RaiseCanExecuteChanged();
        }

        private void OnWatchedFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WatchedFolder folder)
                return;

            if (e.PropertyName == nameof(WatchedFolder.Path))
            {
                _watchedConfig.ClearState(folder);
                ScheduleWatchedFolderSave();
            }
            else if (e.PropertyName == nameof(WatchedFolder.IsEnabled))
            {
                ScheduleWatchedFolderSave();
                _scanAllWatchedFoldersCommand.RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(WatchedFolder.LastScanUtc) ||
                     e.PropertyName == nameof(WatchedFolder.LastScanWasUnchanged))
            {
                ScheduleWatchedFolderSave();
            }
        }

        private void ScheduleWatchedFolderSave()
        {
            if (!_isInitialized || _isRestoringWatchedFolders || _disposed)
                return;

            _ = SaveWatchedFoldersAsync(CancellationToken.None);
        }

        private async Task SaveWatchedFoldersAsync(CancellationToken ct)
        {
            await _watchedFolderSaveGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshot = _watchedConfig.CreateSnapshot();
                await _watchedFolderSettings.SaveAsync(snapshot, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AddViewModel] Failed to save watched folders: {ex}");
            }
            finally
            {
                _watchedFolderSaveGate.Release();
            }
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await action(); }
            finally { IsBusy = false; }
        }

        // ---- wiring to keep Commit button state in sync ----
        private void OnStagingCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (StagingItem item in e.NewItems)
                    item.PropertyChanged += OnStagingItemPropertyChanged;

            if (e.OldItems != null)
                foreach (StagingItem item in e.OldItems)
                    item.PropertyChanged -= OnStagingItemPropertyChanged;

            _commitSelectedCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IndexLabel));
        }

        private void OnStagingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StagingItem.Selected))
                _commitSelectedCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Staging.CollectionChanged -= OnStagingCollectionChanged;

            if (_isInitialized)
            {
                WatchedFolders.CollectionChanged -= OnWatchedFoldersChanged;
                foreach (var folder in WatchedFolders)
                    folder.PropertyChanged -= OnWatchedFolderPropertyChanged;
            }

            _scanner.ItemsStaged -= OnScannerItemsStaged;
            _scanner.Dispose();
        }
    }
}
