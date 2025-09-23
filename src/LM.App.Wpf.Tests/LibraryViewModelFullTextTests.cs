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
using LM.App.Wpf.ViewModels.Library;
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

            Assert.False(vm.Results.ResultsAreFullText);
            Assert.Single(vm.Results.Items);
            var result = vm.Results.Items[0];
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
            vm.Filters.UseFullTextSearch = true;
            vm.Filters.FullTextQuery = "biomarker";

            await InvokeSearchAsync(vm);

            Assert.True(vm.Results.ResultsAreFullText);
            Assert.Single(vm.Results.Items);
            var result = vm.Results.Items[0];
            Assert.Equal("0.420", result.ScoreDisplay);
            Assert.Equal("[biomarker] snippet", result.HighlightDisplay);
            Assert.Same(entry, result.Entry);
        }

        [Fact]
        public void EditCommand_CanExecuteReflectsSelection()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var editor = new RecordingEntryEditor();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, editor);

            Assert.False(vm.Results.EditCommand.CanExecute(null));

            var entry = new Entry { Id = "abc", Title = "Sample" };
            vm.Results.Selected = new LibrarySearchResult(entry, null, null);

            Assert.True(vm.Results.EditCommand.CanExecute(null));
        }

        [Fact]
        public void EditCommand_InvokesEditorForSelectedEntry()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var editor = new RecordingEntryEditor();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, editor);

            var entry = new Entry { Id = "abc", Title = "Editable" };
            vm.Results.Selected = new LibrarySearchResult(entry, null, null);

            vm.Results.EditCommand.Execute(null);

            Assert.Same(entry, editor.LastEdited);
        }

        [Fact]
        public async Task HandleFileDropAsync_AddsAttachmentsAndSavesEntry()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var entry = new Entry { Id = "drop-1", Title = "Doc" };
            store.EntriesById[entry.Id] = entry;

            var storage = new RecordingFileStorageRepository();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, storage: storage);
            var result = new LibrarySearchResult(entry, null, null);
            vm.Results.Items.Add(result);
            vm.Results.Selected = result;

            var filePath = Path.Combine(temp.RootPath, "notes.pdf");
            File.WriteAllText(filePath, "demo");

            await vm.Results.HandleFileDropAsync(new[] { filePath });

            Assert.Equal(1, store.SaveCallCount);
            var savedEntry = store.EntriesById[entry.Id];
            Assert.Single(savedEntry.Attachments);
            var attachment = savedEntry.Attachments[0];
            Assert.Equal(storage.SavedRelativePaths[0], attachment.RelativePath);
            Assert.Equal(Path.Combine("attachments", entry.Id), storage.TargetDirs[0]);
            Assert.Same(vm.Results.Selected, vm.Results.Items[0]);
            Assert.NotSame(result, vm.Results.Selected);
            Assert.Contains(vm.Results.Selected.Entry.Attachments, a => a.RelativePath == attachment.RelativePath);
        }

        [Fact]
        public async Task HandleFileDropAsync_AddsAttachmentsForDropTarget()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var entryA = new Entry { Id = "drop-a", Title = "First" };
            var entryB = new Entry { Id = "drop-b", Title = "Second" };
            store.EntriesById[entryA.Id] = entryA;
            store.EntriesById[entryB.Id] = entryB;

            var storage = new RecordingFileStorageRepository();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, storage: storage);
            var resultA = new LibrarySearchResult(entryA, null, null);
            var resultB = new LibrarySearchResult(entryB, null, null);
            vm.Results.Items.Add(resultA);
            vm.Results.Items.Add(resultB);
            vm.Results.Selected = resultA;

            var filePath = Path.Combine(temp.RootPath, "paper.pdf");
            File.WriteAllText(filePath, "demo");

            await vm.Results.HandleFileDropAsync(new[] { filePath }, resultB);

            Assert.Equal(Path.Combine("attachments", entryB.Id), storage.TargetDirs[0]);
            Assert.Equal(1, store.SaveCallCount);
            Assert.Equal(entryB.Id, vm.Results.Items[1].Entry.Id);
            Assert.Equal(entryB.Id, vm.Results.Selected.Entry.Id);
            Assert.Contains(vm.Results.Selected.Entry.Attachments, a => a.RelativePath == storage.SavedRelativePaths[0]);
        }

        [Fact]
        public async Task HandleFileDropAsync_SkipsDuplicatesWhenRelativePathExists()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var existingPath = Path.Combine("attachments", "dup-1", "notes.pdf");
            var entry = new Entry
            {
                Id = "dup-1",
                Title = "Doc",
                Attachments = new List<Attachment>
                {
                    new Attachment { RelativePath = existingPath }
                }
            };
            store.EntriesById[entry.Id] = entry;

            var storage = new RecordingFileStorageRepository
            {
                PathFactory = _ => existingPath
            };

            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, storage: storage);
            vm.Results.Selected = new LibrarySearchResult(entry, null, null);

            var filePath = Path.Combine(temp.RootPath, "notes.pdf");
            File.WriteAllText(filePath, "dup");

            await vm.Results.HandleFileDropAsync(new[] { filePath });

            Assert.Equal(0, store.SaveCallCount);
            Assert.Single(entry.Attachments);
        }

        [Fact]
        public void CanAcceptFileDrop_RequiresSelectionAndSupportedFile()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var storage = new RecordingFileStorageRepository();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, storage: storage);

            var pdf = Path.Combine(temp.RootPath, "drop.pdf");
            File.WriteAllText(pdf, "pdf");
            var exe = Path.Combine(temp.RootPath, "run.exe");
            File.WriteAllText(exe, "exe");

            Assert.False(vm.Results.CanAcceptFileDrop(new[] { pdf }));

            var entry = new Entry { Id = "sel-1", Title = "Doc" };
            vm.Results.Selected = new LibrarySearchResult(entry, null, null);

            Assert.True(vm.Results.CanAcceptFileDrop(new[] { pdf }));
            Assert.False(vm.Results.CanAcceptFileDrop(new[] { exe }));
        }

        [Fact]
        public void CanAcceptFileDrop_UsesDropTargetWhenSelectionDiffers()
        {
            using var temp = new TempWorkspace();
            var store = new FakeEntryStore();
            var storage = new RecordingFileStorageRepository();
            var vm = CreateViewModel(store, new FakeFullTextSearchService(), temp, storage: storage);

            var entry = new Entry { Id = "target-1", Title = "Drop target" };
            var targetResult = new LibrarySearchResult(entry, null, null);

            var pdf = Path.Combine(temp.RootPath, "drop.pdf");
            File.WriteAllText(pdf, "pdf");

            Assert.True(vm.Results.CanAcceptFileDrop(new[] { pdf }, targetResult));
        }

        private static async Task InvokeSearchAsync(LibraryViewModel vm)
        {
            var method = typeof(LibraryViewModel).GetMethod("SearchAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (Task)method!.Invoke(vm, Array.Empty<object>())!;
            await task.ConfigureAwait(false);
        }

        private static LibraryViewModel CreateViewModel(IEntryStore store,
                                                       IFullTextSearchService search,
                                                       TempWorkspace workspace,
                                                       ILibraryEntryEditor? editor = null,
                                                       IFileStorageRepository? storage = null)
        {
            var ws = new TestWorkspaceService(workspace.RootPath);
            var presetStore = new LibraryFilterPresetStore(ws);
            var prompt = new StubPresetPrompt();
            editor ??= new NoopEntryEditor();
            storage ??= new RecordingFileStorageRepository();
            var filters = new LibraryFiltersViewModel(presetStore, prompt);
            var documents = new NoopDocumentService();
            var results = new LibraryResultsViewModel(store, storage, editor, documents);
            return new LibraryViewModel(store, search, filters, results);
        }

        private sealed class NoopEntryEditor : ILibraryEntryEditor
        {
            public void EditEntry(Entry entry) { }
        }


        private sealed class NoopDocumentService : ILibraryDocumentService
        {
            public void OpenEntry(Entry entry) { }
        }

        private sealed class RecordingEntryEditor : ILibraryEntryEditor
        {
            public Entry? LastEdited { get; private set; }

            public void EditEntry(Entry entry) => LastEdited = entry;
        }

        private sealed class FakeEntryStore : IEntryStore
        {
            public List<Entry> SearchResults { get; } = new();

            public Dictionary<string, Entry> EntriesById { get; } = new(StringComparer.OrdinalIgnoreCase);

            public int SaveCallCount { get; private set; }

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default)
            {
                SaveCallCount++;
                if (string.IsNullOrWhiteSpace(entry.Id))
                    throw new InvalidOperationException("Entry must have an identifier.");
                EntriesById[entry.Id] = entry;
                return Task.CompletedTask;
            }

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

        private sealed class RecordingFileStorageRepository : IFileStorageRepository
        {
            public List<string> TargetDirs { get; } = new();
            public List<string> SavedRelativePaths { get; } = new();
            public Func<string, string>? PathFactory { get; set; }

            public Task<string> SaveNewAsync(string sourcePath, string relativeTargetDir, string? preferredFileName = null, CancellationToken ct = default)
            {
                TargetDirs.Add(relativeTargetDir);
                var fileName = Path.GetFileName(sourcePath) ?? string.Empty;
                var relative = PathFactory?.Invoke(sourcePath)
                    ?? (string.IsNullOrEmpty(relativeTargetDir) ? fileName : Path.Combine(relativeTargetDir, fileName));
                SavedRelativePaths.Add(relative);
                return Task.FromResult(relative);
            }
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
