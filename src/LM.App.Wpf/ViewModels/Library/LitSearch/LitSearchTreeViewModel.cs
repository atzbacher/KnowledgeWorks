using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library.LitSearch;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Library.LitSearch
{
    public sealed partial class LitSearchTreeViewModel : ObservableObject
    {
        private readonly LitSearchOrganizerStore _store;
        private readonly ILibraryPresetPrompt _prompt;
        private readonly IEntryStore _entryStore;
        private readonly IWorkSpaceService _workspace;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public LitSearchTreeViewModel(LitSearchOrganizerStore store,
                                      ILibraryPresetPrompt prompt,

                                      IEntryStore entryStore,
                                      IWorkSpaceService workspace)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            _entryStore = entryStore ?? throw new ArgumentNullException(nameof(entryStore));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            Root = new LitSearchFolderViewModel(this, LitSearchOrganizerFolder.RootId, "LitSearch", isRoot: true);

            CreateFolderCommand = new AsyncRelayCommand<LitSearchFolderViewModel?>(CreateFolderAsync);
            RenameFolderCommand = new AsyncRelayCommand<LitSearchFolderViewModel>(RenameFolderAsync, CanRenameFolder);
            DeleteFolderCommand = new AsyncRelayCommand<LitSearchFolderViewModel>(DeleteFolderAsync, folder => folder is { CanDelete: true });
            MoveCommand = new AsyncRelayCommand<LitSearchDragDropRequest>(MoveAsync, request => request?.Source is not null && request.TargetFolder is not null);
        }

        public LitSearchFolderViewModel Root { get; }

        public IAsyncRelayCommand<LitSearchFolderViewModel?> CreateFolderCommand { get; }

        public IAsyncRelayCommand<LitSearchFolderViewModel> RenameFolderCommand { get; }

        public IAsyncRelayCommand<LitSearchFolderViewModel> DeleteFolderCommand { get; }

        public IAsyncRelayCommand<LitSearchDragDropRequest> MoveCommand { get; }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshots = await LoadEntrySnapshotsAsync(ct).ConfigureAwait(false);
                var synced = await _store.SyncEntriesAsync(snapshots.Keys, ct).ConfigureAwait(false);
                await InvokeOnDispatcherAsync(() =>
                {
                    Root.Children.Clear();
                    foreach (var node in BuildFolderNodes(synced, Root, snapshots))
                    {
                        Root.Children.Add(node);
                    }
                }).ConfigureAwait(false);

                Trace.WriteLine($"[LitSearchTreeViewModel] Refreshed tree with {snapshots.Count} entries.");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private bool CanRenameFolder(LitSearchFolderViewModel? folder)
        {
            return folder is not null && !folder.IsRoot;
        }

        private async Task RenameFolderAsync(LitSearchFolderViewModel? folder)
        {
            if (folder is null || !CanRenameFolder(folder))
            {
                return;
            }

            var newName = await InvokeOnDispatcherAsync(() =>
            {
                return Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name for folder:",
                    "Rename Folder",
                    folder.Name);
            }).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, folder.Name, StringComparison.Ordinal))
            {
                return;
            }

            // Note: You'll need to add RenameFolderAsync to LitSearchOrganizerStore
            // await _store.RenameFolderAsync(folder.Id, newName.Trim(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchTreeViewModel] Rename folder '{folder.Name}' to '{newName}' (not yet implemented in store).");
            await RefreshAsync().ConfigureAwait(false);
        }

        private static Task InvokeOnDispatcherAsync(Func<string> action)
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
        private async Task CreateFolderAsync(LitSearchFolderViewModel? parent)
        {
            var target = parent ?? Root;
            var existingNames = await InvokeOnDispatcherAsync(() => target.Children
                .OfType<LitSearchFolderViewModel>()
                .Select(folder => folder.Name)
                .ToArray()).ConfigureAwait(false);

            var context = new LibraryPresetSaveContext(
                "New folder",
                existingNames,
                "Create LitSearch Folder",
                "Name this folder.");

            var result = await _prompt.RequestSaveAsync(context).ConfigureAwait(false);
            if (result is null || string.IsNullOrWhiteSpace(result.Name))
            {
                return;
            }

            var id = await _store.CreateFolderAsync(target.Id, result.Name.Trim(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchTreeViewModel] Created folder '{result.Name}' ({id}) under '{target.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private bool CanRenameFolder(LitSearchFolderViewModel? folder)
        {
            return folder is not null && !folder.IsRoot;
        }

        private async Task RenameFolderAsync(LitSearchFolderViewModel? folder)
        {
            if (!CanRenameFolder(folder))
            {
                return;
            }

            var siblingNames = await InvokeOnDispatcherAsync(() =>
            {
                var container = folder!.Parent ?? Root;
                return container.Children
                    .OfType<LitSearchFolderViewModel>()
                    .Where(candidate => !string.Equals(candidate.Id, folder.Id, StringComparison.Ordinal))
                    .Select(candidate => candidate.Name)
                    .ToArray();
            }).ConfigureAwait(false);

            var context = new LibraryPresetSaveContext(
                folder.Name,
                siblingNames,
                "Rename Folder",
                "Enter new name for this folder.");

            var result = await _prompt.RequestSaveAsync(context).ConfigureAwait(false);
            if (result is null || string.IsNullOrWhiteSpace(result.Name) || string.Equals(result.Name, folder.Name, StringComparison.Ordinal))
            {
                return;
            }

            await _store.RenameFolderAsync(folder.Id, result.Name.Trim(), CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchTreeViewModel] Renamed folder '{folder.Id}' to '{result.Name}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task DeleteFolderAsync(LitSearchFolderViewModel? folder)
        {
            if (folder is null || !folder.CanDelete)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete folder '{folder.Name}'? Entries will move to the parent folder.",
                "Delete Folder",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await _store.DeleteFolderAsync(folder.Id, CancellationToken.None).ConfigureAwait(false);
            Trace.WriteLine($"[LitSearchTreeViewModel] Deleted folder '{folder.Id}'.");
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task MoveAsync(LitSearchDragDropRequest? request)
        {
            if (request is null || request.Source is null || request.TargetFolder is null)
            {
                return;
            }

            if (request.Source is LitSearchFolderViewModel folder)
            {
                await _store.MoveFolderAsync(folder.Id, request.TargetFolder.Id, request.InsertIndex, CancellationToken.None).ConfigureAwait(false);
                Trace.WriteLine($"[LitSearchTreeViewModel] Requested folder move '{folder.Id}' -> '{request.TargetFolder.Id}' @ {request.InsertIndex}.");
            }
            else if (request.Source is LitSearchEntryViewModel entry)
            {
                await _store.MoveEntryAsync(entry.Id, request.TargetFolder.Id, request.InsertIndex, CancellationToken.None).ConfigureAwait(false);
                Trace.WriteLine($"[LitSearchTreeViewModel] Requested entry move '{entry.Id}' -> '{request.TargetFolder.Id}' @ {request.InsertIndex}.");
            }

            await RefreshAsync().ConfigureAwait(false);
        }

        private IEnumerable<LitSearchNodeViewModel> BuildFolderNodes(LitSearchOrganizerFolder source,
                                                                     LitSearchFolderViewModel parent,
                                                                     IReadOnlyDictionary<string, LitSearchEntrySnapshot> snapshots)
        {
            var nodes = new List<LitSearchNodeViewModel>();

            foreach (var item in source.EnumerateChildren())
            {
                switch (item.Kind)
                {
                    case LitSearchOrganizerNodeKind.Folder when item.Folder is not null:
                    {
                        var folderVm = new LitSearchFolderViewModel(this, item.Folder.Id, item.Folder.Name, isRoot: false)
                        {
                            Parent = parent
                        };

                        foreach (var child in BuildFolderNodes(item.Folder, folderVm, snapshots))
                        {
                            folderVm.Children.Add(child);
                        }

                        nodes.Add(folderVm);
                        break;
                    }

                    case LitSearchOrganizerNodeKind.Entry when item.Entry is not null:
                    {
                        if (!snapshots.TryGetValue(item.Entry.EntryId, out var snapshot))
                        {
                            break;
                        }

                        var entryVm = BuildEntryNode(snapshot, parent);
                        nodes.Add(entryVm);
                        break;
                    }
                }
            }

            return nodes;
        }

        private LitSearchEntryViewModel BuildEntryNode(LitSearchEntrySnapshot snapshot, LitSearchFolderViewModel parent)
        {
            var entryVm = new LitSearchEntryViewModel(this, snapshot.EntryId, snapshot.Title, snapshot.Query)
            {
                Parent = parent
            };

            var entryNode = new LibraryNavigationNodeViewModel(snapshot.Title, LibraryNavigationNodeKind.LitSearchEntry)
            {
                Payload = new LibraryLitSearchEntryPayload(snapshot.EntryId, snapshot.HookPath, snapshot.Title, snapshot.Query)
            };

            entryVm.SetNavigationNode(entryNode);

            foreach (var run in snapshot.Runs)
            {
                var runVm = new LitSearchRunViewModel(this, run.RunId, run.Label, entryVm);
                var runNode = new LibraryNavigationNodeViewModel(run.Label, LibraryNavigationNodeKind.LitSearchRun)
                {
                    Payload = new LibraryLitSearchRunPayload(snapshot.EntryId, run.RunId, run.CheckedEntriesPath, run.Label)
                };

                runVm.SetNavigationNode(runNode);

                entryVm.Runs.Add(runVm);
            }

            return entryVm;
        }

        private async Task<IReadOnlyDictionary<string, LitSearchEntrySnapshot>> LoadEntrySnapshotsAsync(CancellationToken ct)
        {
            var results = new Dictionary<string, LitSearchEntrySnapshot>(StringComparer.Ordinal);
            var workspaceRoot = _workspace.GetWorkspaceRoot();

            await foreach (var entry in _entryStore.EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                if (!IsLitSearchEntry(entry))
                {
                    continue;
                }

                var hookPath = FindLitSearchHookPath(workspaceRoot, entry.Id);
                if (hookPath is null)
                {
                    continue;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(hookPath, ct).ConfigureAwait(false);
                    var hook = JsonSerializer.Deserialize<LitSearchHook>(json, JsonStd.Options);
                    if (hook is null)
                    {
                        continue;
                    }

                    var title = string.IsNullOrWhiteSpace(hook.Title) ? entry.Title ?? entry.Id : hook.Title;
                    var snapshot = new LitSearchEntrySnapshot(entry.Id, title ?? entry.Id, hook.Query, hookPath);

                    foreach (var run in hook.Runs.OrderByDescending(r => r.RunUtc))
                    {
                        if (string.IsNullOrWhiteSpace(run.RunId))
                        {
                            continue;
                        }

                        var label = BuildRunLabel(run);
                        var checkedPath = ResolveCheckedEntriesPath(workspaceRoot, run.CheckedEntryIdsPath);
                        snapshot.Runs.Add(new LitSearchRunSnapshot(run.RunId, label, checkedPath));
                    }

                    results[entry.Id] = snapshot;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[LitSearchTreeViewModel] Failed to read litsearch hook for '{entry.Id}': {ex}");
                }
            }

            return results.ToImmutableDictionary(StringComparer.Ordinal);
        }

        private static string BuildRunLabel(LitSearchRun run)
        {
            var timestamp = run.RunUtc == default ? "Unknown" : run.RunUtc.ToUniversalTime().ToString("u");
            return $"{timestamp} ({run.TotalHits} hits)";
        }

        private static bool IsLitSearchEntry(Entry entry)
        {
            if (entry.Type == EntryType.LitSearch)
            {
                return true;
            }

            return string.Equals(entry.Source, "LitSearch", StringComparison.OrdinalIgnoreCase);
        }

        private static string? FindLitSearchHookPath(string workspaceRoot, string entryId)
        {
            var candidates = new[]
            {
                Path.Combine(workspaceRoot, "entries", entryId, "hooks", "litsearch.json"),
                Path.Combine(workspaceRoot, "entries", entryId, "spokes", "litsearch", "litsearch.json"),
                Path.Combine(workspaceRoot, "entries", entryId, "litsearch", "litsearch.json")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string? ResolveCheckedEntriesPath(string workspaceRoot, string? relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                return null;
            }

            var normalized = relative.Replace('/', Path.DirectorySeparatorChar);
            var combined = Path.Combine(workspaceRoot, normalized);
            return File.Exists(combined) ? combined : null;
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

        private static Task<T> InvokeOnDispatcherAsync<T>(Func<T> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                return dispatcher.InvokeAsync(callback).Task;
            }

            return Task.FromResult(callback());
        }

        private sealed record LitSearchEntrySnapshot(string EntryId, string Title, string? Query, string HookPath)
        {
            public List<LitSearchRunSnapshot> Runs { get; } = new();
        }

        private sealed record LitSearchRunSnapshot(string RunId, string Label, string? CheckedEntriesPath);

    }

    public sealed class LitSearchDragDropRequest
    {
        public LitSearchNodeViewModel? Source { get; init; }

        public LitSearchFolderViewModel? TargetFolder { get; init; }

        public int InsertIndex { get; init; }
    }
}
