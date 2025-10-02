using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Library.LitSearch;
using LM.App.Wpf.ViewModels.Library;
using LM.App.Wpf.ViewModels.Library.LitSearch;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.HubSpoke.Models;
using LM.Infrastructure.FileSystem;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LitSearchTreeViewModelTests
    {
        [Fact]
        public async Task RefreshAsync_BuildsTreeWithRuns()
        {
            using var temp = new TempDir();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var entryStore = new FakeEntryStore();
            var entry = new Entry
            {
                Id = "entry-alpha",
                Title = "Alpha",
                Type = EntryType.LitSearch
            };
            await entryStore.SaveAsync(entry);

            var hookPath = Path.Combine(temp.Path, "entries", entry.Id!, "hooks", "litsearch.json");
            Directory.CreateDirectory(Path.GetDirectoryName(hookPath)!);
            var checkedRelative = Path.Combine("entries", entry.Id!, "hooks", "litsearch_run_checked.json");
            var checkedAbsolute = Path.Combine(temp.Path, checkedRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(checkedAbsolute)!);
            await File.WriteAllTextAsync(checkedAbsolute, "{}");

            var hook = new LitSearchHook
            {
                Title = "Alpha LitSearch",
                Query = "heart disease",
                Runs =
                {
                    new LitSearchRun
                    {
                        RunId = "run-1",
                        RunUtc = DateTime.SpecifyKind(new DateTime(2024, 1, 1, 12, 0, 0), DateTimeKind.Utc),
                        TotalHits = 42,
                        CheckedEntryIdsPath = checkedRelative.Replace(Path.DirectorySeparatorChar, '/')
                    }
                }
            };
            await File.WriteAllTextAsync(hookPath, JsonSerializer.Serialize(hook, JsonStd.Options));

            var organizerStore = new LitSearchOrganizerStore(workspace);
            var prompt = new StubPresetPrompt();
            var tree = new LitSearchTreeViewModel(organizerStore, prompt, entryStore, workspace);

            await tree.RefreshAsync();

            var entryNode = Assert.IsType<LitSearchEntryViewModel>(Assert.Single(tree.Root.Children));
            Assert.Equal("Alpha LitSearch", entryNode.Title);
            Assert.NotNull(entryNode.NavigationNode);
            var entryPayload = Assert.IsType<LibraryLitSearchEntryPayload>(entryNode.NavigationNode!.Payload);
            Assert.Equal("entry-alpha", entryPayload.EntryId);
            Assert.Equal(hookPath, entryPayload.HookPath);

            var runNode = Assert.Single(entryNode.Runs);
            Assert.Equal("run-1", runNode.RunId);
            Assert.NotNull(runNode.NavigationNode);
            var runPayload = Assert.IsType<LibraryLitSearchRunPayload>(runNode.NavigationNode!.Payload);
            Assert.Equal("entry-alpha", runPayload.EntryId);
            Assert.Equal("run-1", runPayload.RunId);
            Assert.Equal(checkedAbsolute, runPayload.CheckedEntriesPath);
        }

        private sealed class StubPresetPrompt : ILibraryPresetPrompt
        {
            public Task<LibraryPresetSaveResult?> RequestSaveAsync(LibraryPresetSaveContext context) => Task.FromResult<LibraryPresetSaveResult?>(null);

            public Task<LibraryPresetSelectionResult?> RequestSelectionAsync(LibraryPresetSelectionContext context) => Task.FromResult<LibraryPresetSelectionResult?>(null);
        }

        private sealed class FakeEntryStore : IEntryStore
        {
            public Dictionary<string, Entry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default)
            {
                Entries[entry.Id!] = entry;
                return Task.CompletedTask;
            }

            public Task SaveAsync(Entry entry) => SaveAsync(entry, CancellationToken.None);

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult(Entries.TryGetValue(id, out var entry) ? entry : null);

            public async IAsyncEnumerable<Entry> EnumerateAsync([EnumeratorCancellation] CancellationToken ct = default)
            {
                foreach (var entry in Entries.Values)
                {
                    yield return entry;
                    await Task.Yield();
                }
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

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "litsearch_vm_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
