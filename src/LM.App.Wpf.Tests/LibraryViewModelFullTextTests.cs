using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using System.Runtime.CompilerServices;

using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Models.Search;
using Xunit;

namespace LM.App.Wpf.Tests
{
    public sealed class LibraryViewModelFullTextTests
    {
        [Fact]
        public async Task MetadataSearch_ProducesEntriesWithoutScores()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var entry = new Entry
            {
                Id = "e1",
                Title = "Test title",
                AddedOnUtc = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc)
            };
            store.SearchResults.Add(entry);

            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp);
            await InvokeSearchAsync(vm);

            Assert.False(vm.ResultsAreFullText);
            Assert.Single(vm.Results);
            var result = vm.Results[0];
            Assert.Same(entry, result.Entry);
            Assert.Null(result.Score);
            Assert.Null(result.Highlight);
        }

        [Fact]
        public async Task FullTextSearch_UsesServiceAndHydratesEntries()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var entry = new Entry
            {
                Id = "doc-1",
                Title = "AI biomarkers",
                AddedOnUtc = DateTime.UtcNow
            };
            store.EntriesById[entry.Id!] = entry;

            var search = new FakeFullTextSearchService
            {
                Hits =
                [
                    new FullTextSearchHit(entry.Id!, 0.42, "[biomarker] snippet")
                ]
            };

            var vm = CreateViewModel(store, search, temp);
            vm.UseFullTextSearch = true;
            vm.FullTextQuery = "biomarker";

            await InvokeSearchAsync(vm);

            Assert.True(vm.ResultsAreFullText);
            Assert.Single(vm.Results);
            var result = vm.Results[0];
            Assert.Equal("0.420", result.ScoreDisplay);
            Assert.Equal("[biomarker] snippet", result.HighlightDisplay);
            Assert.Same(entry, result.Entry);
        }

        private static async Task InvokeSearchAsync(LibraryViewModel vm)
        {
            var method = typeof(LibraryViewModel).GetMethod("SearchAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (Task)method!.Invoke(vm, Array.Empty<object>())!;
            await task.ConfigureAwait(false);
        }

        private static LibraryViewModel CreateViewModel(IEntryStore store, IFullTextSearchService search, TempWorkspace workspace)
        {
            var ws = new TestWorkspaceService(workspace.RootPath);
            var presetStore = new LibraryFilterPresetStore(ws);
            var prompt = new StubPresetPrompt();
            return new LibraryViewModel(store, search, ws, presetStore, prompt);
        }

        private sealed class FakeEntryStore : IEntryStore
        {
            public List<Entry> SearchResults { get; } = new();

            public Dictionary<string, Entry> EntriesById { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default) => throw new NotImplementedException();

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult(EntriesById.TryGetValue(id, out var entry) ? entry : null);


            public async IAsyncEnumerable<Entry> EnumerateAsync([EnumeratorCancellation] CancellationToken ct = default)

            {
                foreach (var entry in EntriesById.Values)
                    yield return entry;
                await Task.CompletedTask;
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
                => Task.FromResult((IReadOnlyList<Entry>)SearchResults);

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default) => throw new NotImplementedException();

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
                => throw new NotImplementedException();

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
                => throw new NotImplementedException();
        }

        private sealed class FakeFullTextSearchService : IFullTextSearchService
        {
            public IReadOnlyList<FullTextSearchHit> Hits { get; set; } = Array.Empty<FullTextSearchHit>();

            public Task<IReadOnlyList<FullTextSearchHit>> SearchAsync(FullTextSearchQuery query, CancellationToken ct = default)
                => Task.FromResult(Hits);
        }

        private sealed class StubPresetPrompt : ILibraryPresetPrompt
        {
            public Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context)
                => Task.FromResult<LibraryPresetSaveResult?>(null);

            public Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context)
                => Task.FromResult<LibraryPresetSelectionResult?>(null);
        }

        private sealed class TestWorkspaceService : IWorkSpaceService
        {
            public TestWorkspaceService(string root)
            {
                WorkspacePath = root;
                Directory.CreateDirectory(root);
            }

            public string? WorkspacePath { get; private set; }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                WorkspacePath = absoluteWorkspacePath;
                Directory.CreateDirectory(absoluteWorkspacePath);
                return Task.CompletedTask;
            }

            public string GetAbsolutePath(string relativePath)
            {
                relativePath ??= string.Empty;
                return Path.Combine(WorkspacePath ?? string.Empty, relativePath);
            }

            public string GetLocalDbPath() => Path.Combine(WorkspacePath ?? string.Empty, "metadata.db");

            public string GetWorkspaceRoot() => WorkspacePath ?? throw new InvalidOperationException("WorkspacePath not set");
        }

        private sealed class TempWorkspace : IDisposable
        {
            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "kw-wpf-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public string RootPath { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath))
                        Directory.Delete(RootPath, true);
                }
                catch
                {
                    // ignore cleanup errors in tests
                }
            }
        }
    }
}
