#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common.Collections;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    public sealed class StagingListViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IAddPipeline _pipeline;
        private readonly RangeObservableCollection<StagingItem> _items = new();
        private static readonly Array s_entryTypes = Enum.GetValues(typeof(EntryType));
        private static readonly int s_maxStageConcurrency = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        private bool _disposed;
        private StagingItem? _current;
        private EntryType _selectedType;

        public event PropertyChangedEventHandler? PropertyChanged;

        public StagingListViewModel(IAddPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _items.CollectionChanged += OnItemsCollectionChanged;
        }

        public ObservableCollection<StagingItem> Items => _items;

        public Array EntryTypes => s_entryTypes;

        public StagingItem? Current
        {
            get => _current;
            set
            {
                if (ReferenceEquals(_current, value))
                    return;

                _current = value;
                if (_current is not null)
                    _selectedType = _current.Type;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedType));
                OnPropertyChanged(nameof(IndexLabel));
            }
        }

        public EntryType SelectedType
        {
            get => _selectedType;
            set
            {
                if (_selectedType == value)
                    return;

                _selectedType = value;
                if (Current is not null)
                    Current.Type = value;

                OnPropertyChanged();
            }
        }

        public string IndexLabel => _items.Count == 0 || Current is null
            ? "0 / 0"
            : $"{_items.IndexOf(Current) + 1} / {_items.Count}";

        public bool HasSelectedItems => _items.Any(static item => item.Selected);

        public bool HasItems => _items.Count > 0;

        public async Task StagePathsAsync(System.Collections.Generic.IEnumerable<string> paths, CancellationToken ct)
        {
            if (paths is null)
                throw new ArgumentNullException(nameof(paths));

            var orderedPaths = NormalizePaths(paths, ct);
            if (orderedPaths.Count == 0)
                return;

            var pending = new Dictionary<int, IReadOnlyList<StagingItem>>();
            var active = new List<Task<(int Index, IReadOnlyList<StagingItem> Items)>>();
            var nextToPublish = 0;
            var nextPathIndex = 0;

            void ScheduleUntilFull()
            {
                while (nextPathIndex < orderedPaths.Count && active.Count < s_maxStageConcurrency)
                {
                    ct.ThrowIfCancellationRequested();

                    var pathIndex = nextPathIndex;
                    var path = orderedPaths[pathIndex];
                    nextPathIndex++;

                    active.Add(StagePathAsync(path, pathIndex, ct));
                }
            }

            ScheduleUntilFull();

            while (active.Count > 0)
            {
                var completed = await Task.WhenAny(active).ConfigureAwait(false);
                active.Remove(completed);

                var (index, items) = await completed.ConfigureAwait(false);
                pending[index] = items;

                while (pending.TryGetValue(nextToPublish, out var ready))
                {
                    pending.Remove(nextToPublish);
                    if (ready.Count > 0)
                    {
                        await AddStagedItemsAsync(ready).ConfigureAwait(false);
                    }

                    nextToPublish++;
                }

                ScheduleUntilFull();
            }
        }

        private async Task<(int Index, IReadOnlyList<StagingItem> Items)> StagePathAsync(string path, int index, CancellationToken ct)
        {
            var staged = await _pipeline.StagePathsAsync(new[] { path }, ct).ConfigureAwait(false);
            return (index, staged);
        }

        private static List<string> NormalizePaths(System.Collections.Generic.IEnumerable<string> paths, CancellationToken ct)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            foreach (var candidate in paths)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var path = candidate.Trim();
                if (!File.Exists(path))
                    continue;

                if (!seen.Add(path))
                    continue;

                ordered.Add(path);
            }

            ordered.Sort(StringComparer.OrdinalIgnoreCase);
            return ordered;
        }

        public Task AddStagedItemsAsync(System.Collections.Generic.IReadOnlyList<StagingItem> items)
        {
            if (items is null || items.Count == 0)
                return Task.CompletedTask;

            void Add()
            {
                _items.AddRange(items);

                if (Current is null)
                    Current = _items.FirstOrDefault();

                OnPropertyChanged(nameof(IndexLabel));
                OnPropertyChanged(nameof(HasSelectedItems));
                OnPropertyChanged(nameof(HasItems));
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

        public Task CommitSelectedAsync(CancellationToken ct)
        {
            var selected = _items.Where(static item => item.Selected).ToList();
            if (selected.Count == 0)
                return Task.CompletedTask;

            return CommitAsync(selected, ct);
        }

        public async Task CommitAsync(IEnumerable<StagingItem> items, CancellationToken ct)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));

            var pending = items
                .Where(static item => item is not null)
                .Distinct()
                .ToList();

            if (pending.Count == 0)
                return;

            var committed = await _pipeline.CommitAsync(pending, ct).ConfigureAwait(false);
            await RemoveCommittedAsync(committed).ConfigureAwait(false);
        }

        private async Task RemoveCommittedAsync(IReadOnlyList<StagingItem> committed)
        {
            if (committed is null || committed.Count == 0)
                return;

            void Remove()
            {
                foreach (var item in committed)
                    _items.Remove(item);

                Current = _items.FirstOrDefault();

                OnPropertyChanged(nameof(IndexLabel));
                OnPropertyChanged(nameof(HasSelectedItems));
                OnPropertyChanged(nameof(HasItems));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                Remove();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                Remove();
                return;
            }

            await dispatcher.InvokeAsync(Remove);
        }

        public void Clear()
        {
            _items.Clear();
            Current = null;
            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasItems));
        }

        public void SelectByOffset(int delta)
        {
            if (_items.Count == 0)
            {
                Current = null;
                return;
            }

            var currentIndex = Current is null ? 0 : _items.IndexOf(Current);
            var newIndex = Math.Clamp(currentIndex + delta, 0, _items.Count - 1);
            Current = _items[newIndex];
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _items.CollectionChanged -= OnItemsCollectionChanged;

            foreach (var item in _items)
                item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (StagingItem item in e.NewItems)
                    item.PropertyChanged += OnItemPropertyChanged;
            }

            if (e.OldItems is not null)
            {
                foreach (StagingItem item in e.OldItems)
                    item.PropertyChanged -= OnItemPropertyChanged;
            }

            if (_items.Count == 0)
            {
                Current = null;
            }
            else if (Current is not null && !_items.Contains(Current))
            {
                Current = _items.FirstOrDefault();
            }

            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasItems));
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StagingItem.Selected))
                OnPropertyChanged(nameof(HasSelectedItems));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
