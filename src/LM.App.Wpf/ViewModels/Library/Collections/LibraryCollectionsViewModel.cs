using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Library.Collections;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.Views.Behaviors;
using LM.Infrastructure.Hooks;

namespace LM.App.Wpf.ViewModels.Library.Collections
{
    public sealed partial class LibraryCollectionsViewModel : ObservableObject
    {
        private readonly LibraryCollectionStore _store;
        private readonly LibraryResultsViewModel _results;
        private readonly HookOrchestrator _hookOrchestrator;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public LibraryCollectionsViewModel(LibraryCollectionStore store,
                                          LibraryResultsViewModel results,
                                          HookOrchestrator hookOrchestrator)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _hookOrchestrator = hookOrchestrator ?? throw new ArgumentNullException(nameof(hookOrchestrator));

            Root = new LibraryCollectionFolderViewModel(this, LibraryCollectionFolder.RootId, "Collections",
                new LibraryCollectionMetadata());

            CreateFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(CreateFolderAsync);
            RenameFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel>(RenameFolderAsync, CanModifyFolder);
            DeleteFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel>(DeleteFolderAsync, CanDeleteFolder);
            AddSelectionToFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel>(AddSelectionToFolderAsync, CanModifyFolder);
            RemoveSelectionFromFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel>(RemoveSelectionFromFolderAsync, CanModifyFolder);
            MoveFolderCommand = new AsyncRelayCommand<CollectionDragDropRequest>(MoveFolderAsync, request => request?.Source is not null);

            _results.SelectionChanged += OnSelectionChanged;
        }

        public LibraryCollectionFolderViewModel Root { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> CreateFolderCommand { get; }
        public IAsyncRelayCommand<LibraryCollectionFolderViewModel> RenameFolderCommand { get; }
        public IAsyncRelayCommand<LibraryCollectionFolderViewModel> DeleteFolderCommand { get; }
        public IAsyncRelayCommand<LibraryCollectionFolderViewModel> AddSelectionToFolderCommand { get; }
        public IAsyncRelayCommand<LibraryCollectionFolderViewModel> RemoveSelectionFromFolderCommand { get; }
        public IAsyncRelayCommand<CollectionDragDropRequest> MoveFolderCommand { get; }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var hierarchy = await _store.GetHierarchyAsync(ct).ConfigureAwait(false);

                await InvokeOnDispatcherAsync(() =>
                {
                    Root.Children.Clear();
                    Root.EntryCount = hierarchy.Entries.Count;
                    Root.Metadata.CreatedBy = hierarchy.Metadata.CreatedBy;
                    Root.Metadata.CreatedUtc = hierarchy.Metadata.CreatedUtc;
                    Root.Metadata.ModifiedBy = hierarchy.Metadata.ModifiedBy;
                    Root.Metadata.ModifiedUtc = hierarchy.Metadata.ModifiedUtc;

                    foreach (var child in hierarchy.Folders)
                    {
                        Root.Children.Add(CreateNode(child));
                    }
                }).ConfigureAwait(false);

                Trace.WriteLine("[LibraryCollectionsViewModel] Refreshed collection hierarchy.");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private LibraryCollectionFolderViewModel CreateNode(LibraryCollectionFolder folder)
        {
            var node = new LibraryCollectionFolderViewModel(this, folder.Id, folder.Name, folder.Metadata.Clone())
            {
                EntryCount = folder.Entries.Count
            };

            foreach (var child in folder.Folders)
            {
                var childNode = CreateNode(child);
                childNode.Parent = node;
                node.Children.Add(childNode);
            }

            return node;
        }

        private async Task CreateFolderAsync(LibraryCollectionFolderViewModel? parent)
        {
            var target = parent ?? Root;
            var baselineName = "New Collection";
            var existingNames = target.Children.Select(child => child.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidate = baselineName;
            var index = 1;

            while (existingNames.Contains(candidate))
            {
                candidate = $"{baselineName} {++index}";
            }

            Trace.WriteLine($"[LibraryCollectionsViewModel] Requesting folder creation '{candidate}' under '{target.Id}'.");
            await _store.CreateFolderAsync(target.Id, candidate, GetCurrentUserName(), CancellationToken.None).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task RenameFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (folder is null || string.Equals(folder.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var newName = await InvokeOnDispatcherAsync(() =>
            {
                var dialog = new Microsoft.VisualBasic.Interaction();
                return Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name for collection:",
                    "Rename Collection",
                    folder.Name);
            }).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, folder.Name, StringComparison.Ordinal))
            {
                return;
            }

            // Note: You'll need to add RenameFolder to LibraryCollectionStore
            // For now, this is a placeholder
            Trace.WriteLine($"[LibraryCollectionsViewModel] Rename '{folder.Name}' to '{newName}' (not yet implemented in store).");
            await RefreshAsync().ConfigureAwait(false);
        }

        private bool CanModifyFolder(LibraryCollectionFolderViewModel? folder)
        {
            if (folder is null)
            {
                return false;
            }

            if (string.Equals(folder.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return false;
            }

            return _results.SelectedItems.Count > 0;
        }

        private static bool CanDeleteFolder(LibraryCollectionFolderViewModel? folder)
        {
            return folder is not null && !string.Equals(folder.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal);
        }

        private async Task DeleteFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (folder is null || string.Equals(folder.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal))
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete collection '{folder.Name}' and all sub-collections?",
                "Delete Collection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await _store.DeleteFolderAsync(folder.Id, GetCurrentUserName(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Deleted folder '{folder.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task AddSelectionToFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (folder is null)
            {
                return;
            }

            var ids = ExtractSelectedEntryIds();
            if (ids.Count == 0)
            {
                return;
            }

            var user = GetCurrentUserName();
            await _store.AddEntriesAsync(folder.Id, ids, user, CancellationToken.None).ConfigureAwait(false);
            await AppendChangeLogAsync(ids, folder, user, "CollectionAdded").ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Added {ids.Count} entry id(s) to '{folder.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task RemoveSelectionFromFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (folder is null)
            {
                return;
            }

            var ids = ExtractSelectedEntryIds();
            if (ids.Count == 0)
            {
                return;
            }

            var user = GetCurrentUserName();
            await _store.RemoveEntriesAsync(folder.Id, ids, user, CancellationToken.None).ConfigureAwait(false);
            await AppendChangeLogAsync(ids, folder, user, "CollectionRemoved").ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Removed {ids.Count} entry id(s) from '{folder.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task MoveFolderAsync(CollectionDragDropRequest? request)
        {
            if (request is null || request.Source is null || request.TargetFolder is null)
            {
                return;
            }

            // Note: You'll need to implement MoveFolderAsync in LibraryCollectionStore
            // This would involve finding the source folder, removing it from its parent,
            // and adding it to the target folder at the specified index
            Trace.WriteLine($"[LibraryCollectionsViewModel] Move folder '{request.Source.Name}' to '{request.TargetFolder.Name}' (not yet implemented).");
            await RefreshAsync().ConfigureAwait(false);
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            AddSelectionToFolderCommand.NotifyCanExecuteChanged();
            RemoveSelectionFromFolderCommand.NotifyCanExecuteChanged();
        }

        private List<string> ExtractSelectedEntryIds()
        {
            return _results.SelectedItems
                .Select(static item => item.InternalId)
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private async Task AppendChangeLogAsync(List<string> entryIds, LibraryCollectionFolderViewModel folder, string user, string action)
        {
            // Hook integration for tracking changes
            foreach (var id in entryIds)
            {
                try
                {
                    await _hookOrchestrator.ExecuteAsync(
                        "CollectionChanged",
                        new { EntryId = id, CollectionId = folder.Id, CollectionName = folder.Name, Action = action, User = user },
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"[LibraryCollectionsViewModel] Hook execution failed: {ex.Message}");
                }
            }
        }

        private static string GetCurrentUserName()
        {
            return Environment.UserName ?? "unknown";
        }

        private static Task InvokeOnDispatcherAsync(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = Application.Current?.Dispatcher;
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

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                return Task.FromResult(action());
            }

            return dispatcher.InvokeAsync(action).Task;
        }
    }
}