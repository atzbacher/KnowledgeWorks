#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    public sealed class StagingListViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IAddPipeline _pipeline;
        private static readonly Array s_entryTypes = Enum.GetValues(typeof(EntryType));
        private bool _disposed;
        private StagingItem? _current;
        private EntryType _selectedType;

        public event PropertyChangedEventHandler? PropertyChanged;

        public StagingListViewModel(IAddPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            Items.CollectionChanged += OnItemsCollectionChanged;
        }

        public ObservableCollection<StagingItem> Items { get; } = new();

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

        public string IndexLabel => Items.Count == 0 || Current is null
            ? "0 / 0"
            : $"{Items.IndexOf(Current) + 1} / {Items.Count}";

        public bool HasSelectedItems => Items.Any(static item => item.Selected);

        public bool HasItems => Items.Count > 0;

        public async Task StagePathsAsync(System.Collections.Generic.IEnumerable<string> paths, CancellationToken ct)
        {
            if (paths is null)
                throw new ArgumentNullException(nameof(paths));

            var staged = await _pipeline.StagePathsAsync(paths, ct).ConfigureAwait(false);
            await AddStagedItemsAsync(staged).ConfigureAwait(false);
        }

        public Task AddStagedItemsAsync(System.Collections.Generic.IReadOnlyList<StagingItem> items)
        {
            if (items is null || items.Count == 0)
                return Task.CompletedTask;

            void Add()
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }

                if (Current is null)
                    Current = Items.FirstOrDefault();

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

        public async Task CommitSelectedAsync(CancellationToken ct)
        {
            var selected = Items.Where(static item => item.Selected).ToList();
            if (selected.Count == 0)
                return;

            var committed = await _pipeline.CommitAsync(selected, ct).ConfigureAwait(false);

            void Remove()
            {
                foreach (var item in committed)
                    Items.Remove(item);

                Current = Items.FirstOrDefault();

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
            Items.Clear();
            Current = null;
            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(HasItems));
        }

        public void SelectByOffset(int delta)
        {
            if (Items.Count == 0)
            {
                Current = null;
                return;
            }

            var currentIndex = Current is null ? 0 : Items.IndexOf(Current);
            var newIndex = Math.Clamp(currentIndex + delta, 0, Items.Count - 1);
            Current = Items[newIndex];
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Items.CollectionChanged -= OnItemsCollectionChanged;

            foreach (var item in Items)
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

            if (Items.Count == 0)
            {
                Current = null;
            }
            else if (Current is not null && !Items.Contains(Current))
            {
                Current = Items.FirstOrDefault();
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
