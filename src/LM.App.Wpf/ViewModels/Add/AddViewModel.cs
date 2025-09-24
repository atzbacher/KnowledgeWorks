#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Common.Dialogs;
using LM.App.Wpf.ViewModels.Dialogs;
using LM.App.Wpf.Views;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Settings;
using LM.HubSpoke.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LM.App.Wpf.ViewModels
{
    public class AddViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly IAddPipeline _pipeline;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider? _dialogServiceProvider;
        private readonly StagingListViewModel _stagingList;
        private readonly WatchedFoldersViewModel _watchedFolders;
        private readonly bool _ownsStagingList;
        private readonly bool _ownsWatchedFolders;
        private readonly SemaphoreSlim _initGate = new(1, 1);
        private readonly RelayCommand _addFilesCommand;
        private readonly RelayCommand _bulkAddFolderCommand;
        private readonly RelayCommand _commitSelectedCommand;
        private readonly RelayCommand _clearCommand;
        private readonly RelayCommand _reviewStagedCommand;
        private bool _isInitialized;
        private bool _disposed;
        private bool _isBusy;

        public AddViewModel(IAddPipeline pipeline,
                            IWorkSpaceService workspace,
                            WatchedFolderScanner? scanner = null,
                            IWatchedFolderSettingsStore? watchedFolderSettings = null,
                            StagingListViewModel? stagingList = null,
                            WatchedFoldersViewModel? watchedFolders = null,
                            IDialogService? dialogService = null)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            if (workspace is null)
                throw new ArgumentNullException(nameof(workspace));

            _stagingList = stagingList ?? new StagingListViewModel(_pipeline);
            _ownsStagingList = stagingList is null;
            _stagingList.PropertyChanged += OnStagingListPropertyChanged;

            if (dialogService is not null)
            {
                _dialogService = dialogService;
            }
            else
            {
                var services = new ServiceCollection();
                services.AddSingleton(_stagingList);
                services.AddTransient<StagingEditorViewModel>();
                services.AddTransient<StagingEditorWindow>();
                _dialogServiceProvider = services.BuildServiceProvider();
                _dialogService = new WpfDialogService(_dialogServiceProvider);
            }

            if (watchedFolders is null)
            {
                var scannerToUse = scanner ?? new WatchedFolderScanner(_pipeline);
                var settingsStore = watchedFolderSettings ?? new JsonWatchedFolderSettingsStore(workspace);
                _watchedFolders = new WatchedFoldersViewModel(_stagingList, scannerToUse, settingsStore, _dialogService);
                _ownsWatchedFolders = true;
            }
            else
            {
                _watchedFolders = watchedFolders;
                _ownsWatchedFolders = false;
            }

            _watchedFolders.SetCommandGuard(RunGuardedAsync);
            _watchedFolders.UpdateParentBusy(_isBusy);

            _addFilesCommand = new RelayCommand(async _ => await RunGuardedAsync(AddFilesAsync), _ => !IsBusy);
            _bulkAddFolderCommand = new RelayCommand(async _ => await RunGuardedAsync(BulkAddFolderAsync), _ => !IsBusy);
            _commitSelectedCommand = new RelayCommand(async _ => await RunGuardedAsync(CommitSelectedAsync), CanCommitSelected);
            _clearCommand = new RelayCommand(_ => ExecuteClear(), _ => !IsBusy);
            _reviewStagedCommand = new RelayCommand(_ => ExecuteReviewStaged(), _ => CanReviewStaged());
        }

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
                                   pmidNormalizer,
                                   simLog),
                   workspace)
        {
        }

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
        {
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized)
                return;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isInitialized)
                    return;

                await _watchedFolders.InitializeAsync(ct).ConfigureAwait(false);
                _isInitialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        public StagingListViewModel StagingListViewModel => _stagingList;
        public WatchedFoldersViewModel WatchedFoldersViewModel => _watchedFolders;

        public ObservableCollection<StagingItem> Staging => _stagingList.Items;
        public ObservableCollection<WatchedFolder> WatchedFolders => _watchedFolders.Folders;
        public Array EntryTypes => _stagingList.EntryTypes;

        public StagingItem? Current
        {
            get => _stagingList.Current;
            set => _stagingList.Current = value;
        }

        public EntryType SelectedType
        {
            get => _stagingList.SelectedType;
            set => _stagingList.SelectedType = value;
        }

        public string IndexLabel => _stagingList.IndexLabel;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => IsBusy = value);
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();

                _addFilesCommand.RaiseCanExecuteChanged();
                _bulkAddFolderCommand.RaiseCanExecuteChanged();
                _commitSelectedCommand.RaiseCanExecuteChanged();
                _clearCommand.RaiseCanExecuteChanged();
                _reviewStagedCommand.RaiseCanExecuteChanged();
                _watchedFolders.UpdateParentBusy(value);
            }
        }

        public System.Windows.Input.ICommand AddFilesCommand => _addFilesCommand;
        public System.Windows.Input.ICommand BulkAddFolderCommand => _bulkAddFolderCommand;
        public System.Windows.Input.ICommand CommitSelectedCommand => _commitSelectedCommand;
        public System.Windows.Input.ICommand ClearCommand => _clearCommand;
        public System.Windows.Input.ICommand ReviewStagedCommand => _reviewStagedCommand;

        public System.Windows.Input.ICommand AddWatchedFolderCommand => _watchedFolders.AddWatchedFolderCommand;
        public System.Windows.Input.ICommand RemoveWatchedFolderCommand => _watchedFolders.RemoveWatchedFolderCommand;
        public System.Windows.Input.ICommand ScanWatchedFolderCommand => _watchedFolders.ScanWatchedFolderCommand;
        public System.Windows.Input.ICommand ScanAllWatchedFoldersCommand => _watchedFolders.ScanAllWatchedFoldersCommand;

        public void SelectByOffset(int delta) => _stagingList.SelectByOffset(delta);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stagingList.PropertyChanged -= OnStagingListPropertyChanged;

            if (_ownsStagingList)
                _stagingList.Dispose();

            if (_ownsWatchedFolders)
                _watchedFolders.Dispose();

            if (_dialogServiceProvider is IDisposable disposable)
                disposable.Dispose();
        }

        private async Task AddFilesAsync()
        {
            var result = _dialogService.ShowOpenFileDialog(new FilePickerOptions
            {
                Filter = "All supported|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.txt;*.md|All files|*.*",
                AllowMultiple = true
            });

            if (result is null || result.Length == 0)
                return;

            await _stagingList.StagePathsAsync(result, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task BulkAddFolderAsync()
        {
            var selected = _dialogService.ShowFolderBrowserDialog(new FolderPickerOptions
            {
                Description = "Select a folder to add"
            });

            if (string.IsNullOrWhiteSpace(selected))
                return;

            var files = Directory.EnumerateFiles(selected, "*.*", SearchOption.AllDirectories);
            await _stagingList.StagePathsAsync(files, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task CommitSelectedAsync()
        {
            await _stagingList.CommitSelectedAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private void ExecuteClear()
        {
            if (IsBusy)
                return;

            _stagingList.Clear();
        }

        private void ExecuteReviewStaged()
        {
            if (IsBusy || !_stagingList.HasItems)
                return;

            _dialogService.ShowStagingEditor(_stagingList);
        }

        private bool CanCommitSelected(object? _) => !IsBusy && _stagingList.HasSelectedItems;
        private bool CanReviewStaged() => !IsBusy && _stagingList.HasItems;

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnStagingListPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StagingListViewModel.HasSelectedItems))
            {
                _commitSelectedCommand.RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(StagingListViewModel.HasItems))
            {
                _reviewStagedCommand.RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(StagingListViewModel.IndexLabel))
            {
                OnPropertyChanged(nameof(IndexLabel));
            }
            else if (e.PropertyName == nameof(StagingListViewModel.Current))
            {
                OnPropertyChanged(nameof(Current));
                OnPropertyChanged(nameof(IndexLabel));
            }
            else if (e.PropertyName == nameof(StagingListViewModel.SelectedType))
            {
                OnPropertyChanged(nameof(SelectedType));
            }
        }
    }
}
