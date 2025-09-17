#nullable enable
using LM.App.Wpf.Common;           // RelayCommand
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // NotifyCollectionChangedEventArgs
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using LM.Infrastructure.Hooks;     // for HookOrchestrator I
using LM.Core.Abstractions;       // for IPmidNormalizerEntryStore, IFileStorageRepository, IHasher, ISimilarityService, IWorkSpaceService, IMetadataExtractor, IDoiNormalizer, IPublicationLookup
using LM.Core.Models;              // EntryType     
using LM.HubSpoke.Abstractions;    // ISimilarityLog

namespace LM.App.Wpf.ViewModels
{
    public class AddViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly IAddPipeline _pipeline;

        // Cache RelayCommand references to avoid repeated casting
        private readonly RelayCommand _addFilesCommand;
        private readonly RelayCommand _bulkAddFolderCommand;
        private readonly RelayCommand _commitSelectedCommand;
        private readonly RelayCommand _clearCommand;

        // Cache EntryTypes array
        private static readonly Array s_entryTypes = Enum.GetValues(typeof(EntryType));

        // ---- Primary ctor (preferred) ----
        public AddViewModel(IAddPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            // Track collection changes AND per-item Selected changes -> keeps Commit button state correct
            Staging.CollectionChanged += OnStagingCollectionChanged;

            // Cache command instances
            _addFilesCommand = new RelayCommand(async _ => await RunGuardedAsync(AddFilesAsync), _ => !IsBusy);
            _bulkAddFolderCommand = new RelayCommand(async _ => await RunGuardedAsync(BulkAddFolderAsync), _ => !IsBusy);
            _commitSelectedCommand = new RelayCommand(async _ => await RunGuardedAsync(CommitSelectedAsync), CanCommitSelected);
            _clearCommand = new RelayCommand(ExecuteClear, _ => !IsBusy);
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
                                   simLog))
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


        // ---------------- UI state ----------------

        public ObservableCollection<StagingItem> Staging { get; } = new();
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
            }
        }

        // Commands
        public ICommand AddFilesCommand => _addFilesCommand;
        public ICommand BulkAddFolderCommand => _bulkAddFolderCommand;
        public ICommand CommitSelectedCommand => _commitSelectedCommand;
        public ICommand ClearCommand => _clearCommand;

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
            var items = await _pipeline.StagePathsAsync(paths, CancellationToken.None);

            foreach (var item in items)
                Staging.Add(item);

            Current ??= Staging.FirstOrDefault();
            OnPropertyChanged(nameof(IndexLabel));
            _commitSelectedCommand.RaiseCanExecuteChanged();
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
    }
}
