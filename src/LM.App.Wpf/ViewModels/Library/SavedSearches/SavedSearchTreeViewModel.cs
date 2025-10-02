using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;

namespace LM.App.Wpf.ViewModels.Library.SavedSearches
{
    public sealed partial class SavedSearchTreeViewModel : ObservableObject
    {
        private readonly LibraryFilterPresetStore _store;
        private readonly ILibraryPresetPrompt _prompt;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        public IAsyncRelayCommand<SavedSearchFolderViewModel> RenameFolderCommand { get; }
        public IAsyncRelayCommand<SavedSearchPresetViewModel> LoadPresetCommand { get; }


        // Update the constructor to initialize these commands:
        public SavedSearchTreeViewModel(LibraryFilterPresetStore store, ILibraryPresetPrompt prompt)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));

            Root = new SavedSearchFolderViewModel(this, LibraryPresetFolder.RootId, "Saved Searches", 0);

            CreateFolderCommand = new AsyncRelayCommand<SavedSearchFolderViewModel?>(CreateFolderAsync);
            RenameFolderCommand = new AsyncRelayCommand<SavedSearchFolderViewModel>(RenameFolderAsync, CanRenameFolder);
            DeleteFolderCommand = new AsyncRelayCommand<SavedSearchFolderViewModel>(DeleteFolderAsync, CanDeleteFolder);
            DeletePresetCommand = new AsyncRelayCommand<SavedSearchPresetViewModel>(DeletePresetAsync, static preset => preset is not null);
            LoadPresetCommand = new AsyncRelayCommand<SavedSearchPresetViewModel>(LoadPresetAsync, static preset => preset is not null);
            MoveCommand = new AsyncRelayCommand<SavedSearchDragDropRequest>(MoveAsync, request => request?.Source is not null);
        }

        public SavedSearchFolderViewModel Root { get; }

        public IAsyncRelayCommand<SavedSearchFolderViewModel?> CreateFolderCommand { get; }

        public IAsyncRelayCommand<SavedSearchFolderViewModel> DeleteFolderCommand { get; }

        public IAsyncRelayCommand<SavedSearchPresetViewModel> DeletePresetCommand { get; }

        public IAsyncRelayCommand<SavedSearchDragDropRequest> MoveCommand { get; }

        public event EventHandler<SavedSearchTreeChangedEventArgs>? TreeChanged;

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var hierarchy = await _store.GetHierarchyAsync(ct).ConfigureAwait(false);
                var summaries = new List<LibraryPresetSummary>();

                await InvokeOnDispatcherAsync(() =>
                {
                    Root.Children.Clear();
                    foreach (var node in BuildNodes(hierarchy, Root, summaries))
                    {
                        Root.Children.Add(node);
                    }
                }).ConfigureAwait(false);

                OnTreeChanged(summaries);
                Trace.WriteLine($"[SavedSearchTreeViewModel] Refreshed hierarchy with {summaries.Count} preset(s).");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task CreateFolderAsync(SavedSearchFolderViewModel? parent)
        {
            var target = parent ?? Root;
            var existing = await InvokeOnDispatcherAsync(() => target.Children
                .OfType<SavedSearchFolderViewModel>()
                .Select(folder => folder.Name)
                .ToArray()).ConfigureAwait(false);

            var context = new LibraryPresetSaveContext(
                "New folder",
                existing,
                "Create Folder",
                "Name this folder.");

            var result = await _prompt.RequestSaveAsync(context).ConfigureAwait(false);
            if (result is null || string.IsNullOrWhiteSpace(result.Name))
            {
                return;
            }

            await _store.CreateFolderAsync(target.Id, result.Name.Trim(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[SavedSearchTreeViewModel] Created folder '{result.Name}' under '{target.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private bool CanDeleteFolder(SavedSearchFolderViewModel? folder)
        {
            return folder is not null && !string.Equals(folder.Id, LibraryPresetFolder.RootId, StringComparison.Ordinal);
        }

        private async Task DeleteFolderAsync(SavedSearchFolderViewModel? folder)
        {
            if (folder is null)
            {
                return;
            }

            if (!CanDeleteFolder(folder))
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete folder '{folder.Name}' and all saved searches within it?",
                "Delete Folder",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await _store.DeleteFolderAsync(folder.Id, CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[SavedSearchTreeViewModel] Deleted folder '{folder.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task DeletePresetAsync(SavedSearchPresetViewModel? preset)
        {
            if (preset is null)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete saved search '{preset.Name}'?",
                "Delete Saved Search",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await _store.DeletePresetAsync(preset.Id, CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[SavedSearchTreeViewModel] Deleted preset '{preset.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task MoveAsync(SavedSearchDragDropRequest? request)
        {
            if (request is null || request.Source is null || request.TargetFolder is null)
            {
                return;
            }

            if (request.Source is SavedSearchFolderViewModel folder)
            {
                await _store.MoveFolderAsync(folder.Id, request.TargetFolder.Id, request.InsertIndex, CancellationToken.None).ConfigureAwait(false);
                Trace.WriteLine($"[SavedSearchTreeViewModel] Requested move of folder '{folder.Id}' to '{request.TargetFolder.Id}' at {request.InsertIndex}.");
            }
            else if (request.Source is SavedSearchPresetViewModel preset)
            {
                await _store.MovePresetAsync(preset.Id, request.TargetFolder.Id, request.InsertIndex, CancellationToken.None).ConfigureAwait(false);
                Trace.WriteLine($"[SavedSearchTreeViewModel] Requested move of preset '{preset.Id}' to '{request.TargetFolder.Id}' at {request.InsertIndex}.");
            }

            await RefreshAsync().ConfigureAwait(false);
        }

        private IEnumerable<SavedSearchNodeViewModel> BuildNodes(LibraryPresetFolder source,
                                                                  SavedSearchFolderViewModel parent,
                                                                  List<LibraryPresetSummary> summaries)
        {
            var nodes = new List<SavedSearchNodeViewModel>();

            foreach (var item in source.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LibraryPresetNodeKind.Folder when item.Folder is not null:
                        {
                            var folderVm = new SavedSearchFolderViewModel(this, item.Folder.Id, item.Folder.Name, item.Folder.SortOrder)
                            {
                                Parent = parent
                            };

                            foreach (var child in BuildNodes(item.Folder, folderVm, summaries))
                            {
                                folderVm.Children.Add(child);
                            }

                            nodes.Add(folderVm);
                            break;
                        }

                    case LibraryPresetNodeKind.Preset when item.Preset is not null:
                        {
                            var presetVm = new SavedSearchPresetViewModel(this, item.Preset, item.Preset.SortOrder)
                            {
                                Parent = parent
                            };

                            summaries.Add(presetVm.ToSummary());
                            nodes.Add(presetVm);
                            break;
                        }
                }
            }

            return nodes
                .OrderBy(static node => node.SortOrder)
                .ToList();
        }

        private void OnTreeChanged(IReadOnlyList<LibraryPresetSummary> summaries)
        {
            TreeChanged?.Invoke(this, new SavedSearchTreeChangedEventArgs(summaries.ToArray()));
        }

        private static Task InvokeOnDispatcherAsync(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        private static Task<TResult> InvokeOnDispatcherAsync<TResult>(Func<TResult> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                return Task.FromResult(action());
            }

            return dispatcher.InvokeAsync(action).Task;
        }
    }

    public sealed class SavedSearchTreeChangedEventArgs : EventArgs
    {
        public SavedSearchTreeChangedEventArgs(IReadOnlyList<LibraryPresetSummary> presets)
        {
            Presets = presets ?? Array.Empty<LibraryPresetSummary>();
        }

        public IReadOnlyList<LibraryPresetSummary> Presets { get; }
    }

    public sealed class SavedSearchDragDropRequest
    {
        public SavedSearchNodeViewModel? Source { get; init; }

        public SavedSearchFolderViewModel? TargetFolder { get; init; }

        public int InsertIndex { get; init; }


        private bool CanRenameFolder(SavedSearchFolderViewModel? folder)
        {
            return folder is not null && !string.Equals(folder.Id, LibraryPresetFolder.RootId, StringComparison.Ordinal);
        }

        private async Task RenameFolderAsync(SavedSearchFolderViewModel? folder)
        {
            if (folder is null || !CanRenameFolder(folder))
            {
                return;
            }

            var existing = await InvokeOnDispatcherAsync(() => folder.Parent?.Children
                .OfType<SavedSearchFolderViewModel>()
                .Where(f => f.Id != folder.Id)
                .Select(f => f.Name)
                .ToArray() ?? Array.Empty<string>()).ConfigureAwait(false);

            var context = new LibraryPresetSaveContext(
                folder.Name,
                existing,
                "Rename Folder",
                "Enter new name for this folder.");

            var result = await _prompt.RequestSaveAsync(context).ConfigureAwait(false);
            if (result is null || string.IsNullOrWhiteSpace(result.Name) || string.Equals(result.Name, folder.Name, StringComparison.Ordinal))
            {
                return;
            }

            await _store.RenameFolderAsync(folder.Id, result.Name.Trim(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[SavedSearchTreeViewModel] Renamed folder '{folder.Id}' to '{result.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task LoadPresetAsync(SavedSearchPresetViewModel? preset)
        {
            if (preset is null)
            {
                return;
            }

            // The preset will be loaded by the LibraryFiltersViewModel
            // We just need to trigger the event or notify
            Trace.WriteLine($"[SavedSearchTreeViewModel] Load preset '{preset.Name}' requested.");

            // Note: This should be handled by the parent view model or through event aggregation
            // For now, we'll just log it. The actual loading is done in LibraryView.xaml.cs OnSavedSearchSelected
        }
    }
}