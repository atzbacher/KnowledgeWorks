using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Library.Collections;
using LM.App.Wpf.ViewModels.Library;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

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

            Root = new LibraryCollectionFolderViewModel(this, LibraryCollectionFolder.RootId, "Collections", new LibraryCollectionMetadata());

            CreateFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(CreateFolderAsync);
            RenameFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(RenameFolderAsync, canExecute: CanRenameFolder);
            DeleteFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(DeleteFolderAsync, canExecute: CanDeleteFolder);
            AddSelectionToFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(AddSelectionToFolderAsync, canExecute: CanModifyFolder);
            RemoveSelectionFromFolderCommand = new AsyncRelayCommand<LibraryCollectionFolderViewModel?>(RemoveSelectionFromFolderAsync, canExecute: CanModifyFolder);
            MoveFolderCommand = new AsyncRelayCommand<CollectionDragDropRequest?>(MoveFolderAsync, canExecute: request => request?.Source is not null && request.TargetFolder is not null);

            _results.SelectionChanged += OnSelectionChanged;
        }

        public LibraryCollectionFolderViewModel Root { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> CreateFolderCommand { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> RenameFolderCommand { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> DeleteFolderCommand { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> AddSelectionToFolderCommand { get; }

        public IAsyncRelayCommand<LibraryCollectionFolderViewModel?> RemoveSelectionFromFolderCommand { get; }

        public IAsyncRelayCommand<CollectionDragDropRequest?> MoveFolderCommand { get; }

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

        private bool CanRenameFolder(LibraryCollectionFolderViewModel? folder)
        {
            return folder is not null && !string.Equals(folder.Id, LibraryCollectionFolder.RootId, StringComparison.Ordinal);
        }

        private async Task RenameFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (!CanRenameFolder(folder))
            {
                return;
            }

            var siblings = await InvokeOnDispatcherAsync(() =>
            {
                var container = folder!.Parent ?? Root;
                return container.Children
                    .Where(child => child is LibraryCollectionFolderViewModel candidate && !string.Equals(candidate.Id, folder.Id, StringComparison.Ordinal))
                    .Select(child => child.Name)
                    .ToArray();
            }).ConfigureAwait(false);

            var newName = await InvokeOnDispatcherAsync(() =>
            {
                return Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name for collection:",
                    "Rename Collection",
                    folder!.Name);
            }).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, folder.Name, StringComparison.Ordinal))
            {
                Trace.WriteLine("[LibraryCollectionsViewModel] Rename cancelled or unchanged.");
                return;
            }

            if (siblings.Contains(newName, StringComparer.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"[LibraryCollectionsViewModel] Rename blocked because '{newName}' already exists.");
                return;
            }

            await _store.RenameFolderAsync(folder.Id, newName.Trim(), GetCurrentUserName(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Renamed folder '{folder.Id}' to '{newName}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private bool CanModifyFolder(LibraryCollectionFolderViewModel? folder)
        {
            if (!CanRenameFolder(folder))
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
            if (!CanDeleteFolder(folder))
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete collection '{folder!.Name}' and remove all its entry references?",
                "Delete Collection",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                Trace.WriteLine("[LibraryCollectionsViewModel] Delete cancelled by user.");
                return;
            }

            await _store.DeleteFolderAsync(folder.Id, GetCurrentUserName(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Deleted folder '{folder.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task AddSelectionToFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (!CanModifyFolder(folder))
            {
                return;
            }

            var ids = ExtractSelectedEntryIds();
            if (ids.Count == 0)
            {
                Trace.WriteLine("[LibraryCollectionsViewModel] AddSelectionToFolderAsync skipped - no entry ids selected.");
                return;
            }

            var user = GetCurrentUserName();
            await _store.AddEntriesAsync(folder!.Id, ids, user, CancellationToken.None).ConfigureAwait(false);
            await AppendChangeLogAsync(ids, folder, user, "CollectionAdded").ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Added {ids.Count} entry id(s) to '{folder.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task RemoveSelectionFromFolderAsync(LibraryCollectionFolderViewModel? folder)
        {
            if (!CanModifyFolder(folder))
            {
                return;
            }

            var ids = ExtractSelectedEntryIds();
            if (ids.Count == 0)
            {
                Trace.WriteLine("[LibraryCollectionsViewModel] RemoveSelectionFromFolderAsync skipped - no entry ids selected.");
                return;
            }

            var user = GetCurrentUserName();
            await _store.RemoveEntriesAsync(folder!.Id, ids, user, CancellationToken.None).ConfigureAwait(false);
            await AppendChangeLogAsync(ids, folder, user, "CollectionRemoved").ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Removed {ids.Count} entry id(s) from '{folder.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task MoveFolderAsync(CollectionDragDropRequest? request)
        {
            if (request?.Source is null || request.TargetFolder is null)
            {
                Trace.WriteLine("[LibraryCollectionsViewModel] MoveFolderAsync skipped - invalid drag/drop request.");
                return;
            }

            await _store.MoveFolderAsync(request.Source.Id, request.TargetFolder.Id, request.InsertIndex, GetCurrentUserName(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LibraryCollectionsViewModel] Requested folder move '{request.Source.Id}' -> '{request.TargetFolder.Id}' @ {request.InsertIndex}.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            AddSelectionToFolderCommand.NotifyCanExecuteChanged();
            RemoveSelectionFromFolderCommand.NotifyCanExecuteChanged();
        }

        private IReadOnlyList<string> ExtractSelectedEntryIds()
        {
            return _results.SelectedItems
                .Select(item => item.Entry?.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task AppendChangeLogAsync(IReadOnlyList<string> ids,
                                                LibraryCollectionFolderViewModel folder,
                                                string performedBy,
                                                string action)
        {
            foreach (var entryId in ids)
            {
                var hook = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new HookM.EntryChangeLogEvent
                        {
                            PerformedBy = performedBy,
                            Action = $"{action}:{folder.Name}",
                            TimestampUtc = DateTime.UtcNow
                        }
                    }
                };

                var context = new HookContext
                {
                    ChangeLog = hook
                };

                await _hookOrchestrator.ProcessAsync(entryId, context, CancellationToken.None).ConfigureAwait(false);
                Trace.WriteLine($"[LibraryCollectionsViewModel] Appended changelog event '{action}' for entry '{entryId}'.");
            }
        }

        private static Task InvokeOnDispatcherAsync(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(action).Task;
            }

            action();
            return Task.CompletedTask;
        }

        private static Task<T> InvokeOnDispatcherAsync<T>(Func<T> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(action).Task;
            }

            return Task.FromResult(action());
        }

        private static string GetCurrentUserName()
        {
            var user = Environment.UserName;
            return string.IsNullOrWhiteSpace(user) ? "unknown" : user;
        }
    }

    public sealed class CollectionDragDropRequest
    {
        public LibraryCollectionFolderViewModel? Source { get; init; }

        public LibraryCollectionFolderViewModel? TargetFolder { get; init; }

        public int InsertIndex { get; init; }
    }
}
