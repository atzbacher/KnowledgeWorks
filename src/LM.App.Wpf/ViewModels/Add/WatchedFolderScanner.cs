#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels
{
    /// <summary>Watches configured folders and stages updated files through the add pipeline.</summary>
    public sealed class WatchedFolderScanner : IDisposable
    {
        private readonly IAddPipeline _pipeline;
        private readonly TimeSpan _debounce = TimeSpan.FromSeconds(1.5);
        private readonly Dictionary<WatchedFolder, FolderSubscription> _subscriptions = new();
        private readonly object _gate = new();
        private bool _disposed;
        private WatchedFolderConfig? _config;

        public WatchedFolderScanner(IAddPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public event EventHandler<WatchedFolderScanEventArgs>? ItemsStaged;

        public void Attach(WatchedFolderConfig config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            lock (_gate)
            {
                if (_config is not null)
                    throw new InvalidOperationException("Scanner already attached.");
                _config = config;
            }

            config.Folders.CollectionChanged += OnFoldersChanged;
            foreach (var folder in config.Folders)
            {
                StartTracking(folder);
            }
        }

        public async Task ScanAsync(WatchedFolder? folder, CancellationToken ct)
        {
            if (folder is null) return;
            if (string.IsNullOrWhiteSpace(folder.Path)) return;
            if (!Directory.Exists(folder.Path)) return;

            try
            {
                var files = Directory.EnumerateFiles(folder.Path, "*.*", SearchOption.AllDirectories);
                var staged = await _pipeline.StagePathsAsync(files, ct).ConfigureAwait(false);
                folder.MarkScanned(DateTimeOffset.UtcNow);
                if (staged.Count > 0)
                {
                    OnItemsStaged(folder, staged);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFolderScanner] Failed to scan '{folder.Path}': {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_config is not null)
            {
                _config.Folders.CollectionChanged -= OnFoldersChanged;
                foreach (var folder in _config.Folders)
                {
                    folder.PropertyChanged -= OnFolderPropertyChanged;
                }
            }

            foreach (var entry in _subscriptions.Values)
            {
                entry.Dispose();
            }
            _subscriptions.Clear();
        }

        private void OnFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (WatchedFolder folder in e.NewItems)
                {
                    StartTracking(folder);
                }
            }

            if (e.OldItems is not null)
            {
                foreach (WatchedFolder folder in e.OldItems)
                {
                    StopTracking(folder);
                }
            }
        }

        private void StartTracking(WatchedFolder folder)
        {
            folder.PropertyChanged += OnFolderPropertyChanged;
            if (folder.IsEnabled)
            {
                TryEnable(folder);
            }
        }

        private void StopTracking(WatchedFolder folder)
        {
            folder.PropertyChanged -= OnFolderPropertyChanged;
            Disable(folder);
        }

        private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WatchedFolder folder) return;
            if (e.PropertyName == nameof(WatchedFolder.IsEnabled))
            {
                if (folder.IsEnabled)
                {
                    TryEnable(folder);
                }
                else
                {
                    Disable(folder);
                }
            }
            else if (e.PropertyName == nameof(WatchedFolder.Path))
            {
                Disable(folder);
                if (folder.IsEnabled)
                {
                    TryEnable(folder);
                }
            }
        }

        private void TryEnable(WatchedFolder folder)
        {
            if (_subscriptions.ContainsKey(folder))
                return;
            if (string.IsNullOrWhiteSpace(folder.Path))
                return;
            if (!Directory.Exists(folder.Path))
                return;

            try
            {
                var subscription = new FolderSubscription(folder, ProcessPathsAsync, _debounce);
                _subscriptions[folder] = subscription;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WatchedFolderScanner] Failed to watch '{folder.Path}': {ex}");
            }
        }

        private void Disable(WatchedFolder folder)
        {
            if (_subscriptions.Remove(folder, out var subscription))
            {
                subscription.Dispose();
            }
        }

        private Task ProcessPathsAsync(WatchedFolder folder, IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                try
                {
                    var staged = await _pipeline.StagePathsAsync(paths, CancellationToken.None).ConfigureAwait(false);
                    folder.MarkScanned(DateTimeOffset.UtcNow);
                    if (staged.Count > 0)
                    {
                        OnItemsStaged(folder, staged);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[WatchedFolderScanner] Failed to stage changes for '{folder.Path}': {ex}");
                }
            });
        }

        private void OnItemsStaged(WatchedFolder folder, IReadOnlyList<StagingItem> items)
        {
            ItemsStaged?.Invoke(this, new WatchedFolderScanEventArgs(folder, items));
        }

        private sealed class FolderSubscription : IDisposable
        {
            private readonly WatchedFolder _folder;
            private readonly Func<WatchedFolder, IReadOnlyList<string>, Task> _flush;
            private readonly TimeSpan _debounce;
            private readonly object _gate = new();
            private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
            private readonly FileSystemWatcher _watcher;
            private readonly Timer _timer;
            private bool _disposed;

            public FolderSubscription(WatchedFolder folder, Func<WatchedFolder, IReadOnlyList<string>, Task> flush, TimeSpan debounce)
            {
                _folder = folder;
                _flush = flush;
                _debounce = debounce;

                _watcher = new FileSystemWatcher(folder.Path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };
                _watcher.Created += OnChanged;
                _watcher.Changed += OnChanged;
                _watcher.Renamed += OnRenamed;
                _watcher.EnableRaisingEvents = true;

                _timer = new Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnChanged;
                _watcher.Changed -= OnChanged;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();

                lock (_gate)
                {
                    _pending.Clear();
                }

                _timer.Dispose();
            }

            private void OnChanged(object sender, FileSystemEventArgs e)
                => QueuePath(e.FullPath);

            private void OnRenamed(object sender, RenamedEventArgs e)
                => QueuePath(e.FullPath);

            private void QueuePath(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;
                if (!File.Exists(path))
                    return;

                lock (_gate)
                {
                    _pending.Add(path);
                    _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
                }
            }

            private void OnTimer(object? state)
            {
                string[] paths;
                lock (_gate)
                {
                    if (_pending.Count == 0)
                        return;
                    paths = _pending.ToArray();
                    _pending.Clear();
                }

                _ = _flush(_folder, paths);
            }
        }
    }

    public sealed class WatchedFolderScanEventArgs : EventArgs
    {
        public WatchedFolderScanEventArgs(WatchedFolder folder, IReadOnlyList<StagingItem> items)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public WatchedFolder Folder { get; }

        public IReadOnlyList<StagingItem> Items { get; }
    }
}
