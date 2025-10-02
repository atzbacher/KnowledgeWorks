#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Views;
using LM.App.Wpf.Views.Library.Controls;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryViewInteractionsTests
    {
        [Fact]
        public async Task UnifiedQueryBoxEnter_InvokesSearchCommand()
        {
            var vm = new StubLibraryViewModel();

            await RunOnStaThreadAsync(() =>
            {
                EnsureApplication();
                var view = new LibraryView
                {
                    DataContext = vm
                };

                InitializeView(view);

                var queryBox = FindDescendant<LibrarySearchQueryBox>(view, box => string.Equals(box.Name, "UnifiedQueryBox", StringComparison.Ordinal));
                Assert.NotNull(queryBox);

                var binding = queryBox!.InputBindings
                    .OfType<System.Windows.Input.KeyBinding>()
                    .FirstOrDefault(static b => b.Key == System.Windows.Input.Key.Enter);

                Assert.NotNull(binding);
                Assert.NotNull(binding!.Command);

                binding.Command!.Execute(null);

                Assert.Equal(1, vm.SearchInvocationCount);
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task FullTextToggle_ExecutesSearchCommandOnStateChange()
        {
            var vm = new StubLibraryViewModel();

            await RunOnStaThreadAsync(() =>
            {
                EnsureApplication();
                var view = new LibraryView
                {
                    DataContext = vm
                };

                InitializeView(view);

                var toggle = FindDescendant<System.Windows.Controls.Primitives.ToggleButton>(view, control => string.Equals(control.Name, "FullTextToggle", StringComparison.Ordinal));
                Assert.NotNull(toggle);

                toggle!.IsChecked = true;
                Assert.True(vm.Filters.UseFullTextSearch);
                Assert.Equal(1, vm.SearchInvocationCount);

                toggle.IsChecked = false;
                Assert.False(vm.Filters.UseFullTextSearch);
                Assert.Equal(2, vm.SearchInvocationCount);
            }).ConfigureAwait(false);
        }

        private static void InitializeView(LibraryView view)
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            view.Measure(new System.Windows.Size(800, 600));
            view.Arrange(new System.Windows.Rect(0, 0, 800, 600));
            view.UpdateLayout();
        }

        private static void EnsureApplication()
        {
            if (System.Windows.Application.Current is null)
            {
                _ = new System.Windows.Application();
            }
        }

        private static T? FindDescendant<T>(System.Windows.DependencyObject root, Func<T, bool>? predicate = null)
            where T : class
        {
            if (root is null)
            {
                return null;
            }

            var queue = new Queue<System.Windows.DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next is T candidate && (predicate is null || predicate(candidate)))
                {
                    return candidate;
                }

                var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(next);
                for (var i = 0; i < count; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(next, i);
                    if (child is not null)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return null;
        }

        private static Task RunOnStaThreadAsync(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    completion.SetResult(true);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return completion.Task;
        }

        private sealed class StubLibraryViewModel
        {
            private readonly AsyncRelayCommand _command;

            public StubLibraryViewModel()
            {
                Filters = new StubFilters();
                _command = new AsyncRelayCommand(async () =>
                {
                    SearchInvocationCount++;
                    await Task.CompletedTask;
                });
            }

            public StubFilters Filters { get; }

            public IAsyncRelayCommand SearchCommand => _command;

            public int SearchInvocationCount { get; private set; }
        }

        private sealed class StubFilters : ObservableObject
        {
            private string? _unifiedQuery = string.Empty;
            private bool _useFullTextSearch;

            public string? UnifiedQuery
            {
                get => _unifiedQuery;
                set => SetProperty(ref _unifiedQuery, value);
            }

            public bool UseFullTextSearch
            {
                get => _useFullTextSearch;
                set => SetProperty(ref _useFullTextSearch, value);
            }
        }
    }
}
