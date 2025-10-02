using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryFiltersViewModelTests : IDisposable
    {
        private readonly TestEnvironment _environment = new();

        [Fact]
        public void Clear_ResetsSearchStateAndFilters()
        {
            var vm = _environment.CreateViewModel();

            vm.UseFullTextSearch = true;
            vm.UnifiedQuery = "title:heart";
            vm.FullTextQuery = "heart";
            vm.DateFrom = new DateTime(2020, 1, 1);
            vm.DateTo = new DateTime(2021, 6, 1);
            vm.SelectedSort = LibrarySortOptions.TitleAscending;
            vm.SelectedTags.Add("alpha");
            vm.SelectedTags.Add("beta");

            vm.Clear();

            Assert.False(vm.UseFullTextSearch);
            Assert.Equal(string.Empty, vm.UnifiedQuery);
            Assert.Equal(string.Empty, vm.FullTextQuery);
            Assert.Null(vm.DateFrom);
            Assert.Null(vm.DateTo);
            Assert.Equal(LibrarySortOptions.NewestFirst, vm.SelectedSort);
            Assert.Empty(vm.SelectedTags);
        }

        [Fact]
        public void CaptureAndApplyState_RoundTripsCoreFilters()
        {
            var vm = _environment.CreateViewModel();

            vm.UseFullTextSearch = true;
            vm.UnifiedQuery = "fulltext biomarkers";
            vm.FullTextQuery = "biomarkers";
            vm.DateFrom = new DateTime(2022, 2, 2);
            vm.DateTo = new DateTime(2023, 3, 3);
            vm.SelectedSort = LibrarySortOptions.TitleDescending;
            vm.SelectedTags.Add("neuro");
            vm.SelectedTags.Add("vision");

            var snapshot = vm.CaptureState();

            vm.UseFullTextSearch = false;
            vm.UnifiedQuery = string.Empty;
            vm.FullTextQuery = string.Empty;
            vm.DateFrom = null;
            vm.DateTo = null;
            vm.SelectedSort = LibrarySortOptions.NewestFirst;
            vm.SelectedTags.Clear();

            vm.ApplyState(snapshot);

            Assert.True(vm.UseFullTextSearch);
            Assert.Equal("fulltext biomarkers", vm.UnifiedQuery);
            Assert.Equal("biomarkers", vm.FullTextQuery);
            Assert.Equal(new DateTime(2022, 2, 2), vm.DateFrom);
            Assert.Equal(new DateTime(2023, 3, 3), vm.DateTo);
            Assert.Equal(LibrarySortOptions.TitleDescending, vm.SelectedSort);
            Assert.Equal(new[] { "neuro", "vision" }, vm.SelectedTags);
        }

        public void Dispose()
        {
            _environment.Dispose();
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly string _workspaceRoot;

            public TestEnvironment()
            {
                _workspaceRoot = Path.Combine(Path.GetTempPath(), "kw-libfilters-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_workspaceRoot);
                Directory.CreateDirectory(Path.Combine(_workspaceRoot, "library"));
            }

            public LibraryFiltersViewModel CreateViewModel()
            {
                var workspace = new TestWorkspaceService(_workspaceRoot);
                var presetStore = new LibraryFilterPresetStore(workspace);
                var prompt = new StubPresetPrompt();
                var store = new StubEntryStore();
                return new LibraryFiltersViewModel(presetStore, prompt, store, workspace);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_workspaceRoot))
                    {
                        Directory.Delete(_workspaceRoot, recursive: true);
                    }
                }
                catch
                {
                    // best effort cleanup
                }
            }
        }

        private sealed class StubPresetPrompt : ILibraryPresetPrompt
        {
            public Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context)
                => Task.FromResult<LibraryPresetSaveResult?>(null);

            public Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context)
                => Task.FromResult<LibraryPresetSelectionResult?>(null);
        }

        private sealed class StubEntryStore : IEntryStore
        {
            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default) => Task.CompletedTask;

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);

            public async IAsyncEnumerable<Entry> EnumerateAsync([EnumeratorCancellation] CancellationToken ct = default)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
                => Task.FromResult<Entry?>(null);
        }

        private sealed class TestWorkspaceService : IWorkSpaceService
        {
            public TestWorkspaceService(string root)
            {
                WorkspacePath = root;
            }

            public string? WorkspacePath { get; private set; }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                WorkspacePath = absoluteWorkspacePath;
                Directory.CreateDirectory(absoluteWorkspacePath);
                Directory.CreateDirectory(Path.Combine(absoluteWorkspacePath, "library"));
                return Task.CompletedTask;
            }

            public string GetWorkspaceRoot()
            {
                if (WorkspacePath is null)
                {
                    throw new InvalidOperationException("WorkspacePath has not been initialized.");
                }

                return WorkspacePath;
            }

            public string GetLocalDbPath()
            {
                return Path.Combine(WorkspacePath ?? string.Empty, "metadata.db");
            }

            public string GetAbsolutePath(string relativePath)
            {
                relativePath ??= string.Empty;
                return Path.Combine(WorkspacePath ?? string.Empty, relativePath);
            }
        }
    }
}
