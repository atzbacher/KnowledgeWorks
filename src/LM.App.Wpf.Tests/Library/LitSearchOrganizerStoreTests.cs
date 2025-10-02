using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LM.App.Wpf.Library.LitSearch;
using LM.Infrastructure.FileSystem;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LitSearchOrganizerStoreTests
    {
        [Fact]
        public async Task SyncEntriesAsync_AddsAndRemovesEntries()
        {
            using var temp = new TempDir();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var store = new LitSearchOrganizerStore(workspace);
            var first = await store.SyncEntriesAsync(new[] { "alpha", "beta" });
            Assert.Equal(2, first.Entries.Count);
            Assert.Contains(first.Entries, entry => entry.EntryId == "alpha");
            Assert.Contains(first.Entries, entry => entry.EntryId == "beta");

            var second = await store.SyncEntriesAsync(new[] { "beta", "gamma" });
            Assert.Equal(2, second.Entries.Count);
            Assert.DoesNotContain(second.Entries, entry => entry.EntryId == "alpha");
            Assert.Contains(second.Entries, entry => entry.EntryId == "beta");
            Assert.Contains(second.Entries, entry => entry.EntryId == "gamma");
        }

        [Fact]
        public async Task MoveEntryAndFolder_PersistsHierarchy()
        {
            using var temp = new TempDir();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var store = new LitSearchOrganizerStore(workspace);
            await store.SyncEntriesAsync(new[] { "alpha", "beta" });

            var folderId = await store.CreateFolderAsync(LitSearchOrganizerFolder.RootId, "Folder", default);
            await store.MoveEntryAsync("alpha", folderId, 0);

            var hierarchy = await store.GetHierarchyAsync();
            var folder = Assert.Single(hierarchy.Folders);
            Assert.Equal("Folder", folder.Name);
            var entry = Assert.Single(folder.Entries);
            Assert.Equal("alpha", entry.EntryId);

            await store.MoveFolderAsync(folderId, LitSearchOrganizerFolder.RootId, 0);
            var updated = await store.GetHierarchyAsync();
            var rootFolder = Assert.Single(updated.Folders);
            Assert.Equal(folderId, rootFolder.Id);
            Assert.Single(rootFolder.Entries);
        }

        [Fact]
        public async Task DeleteFolder_MovesEntriesToParent()
        {
            using var temp = new TempDir();
            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var store = new LitSearchOrganizerStore(workspace);
            await store.SyncEntriesAsync(new[] { "alpha", "beta" });
            var folderId = await store.CreateFolderAsync(LitSearchOrganizerFolder.RootId, "Folder", default);
            await store.MoveEntryAsync("alpha", folderId, 0);

            await store.DeleteFolderAsync(folderId);
            var hierarchy = await store.GetHierarchyAsync();
            Assert.Empty(hierarchy.Folders);
            Assert.Equal(2, hierarchy.Entries.Count);
            Assert.Contains(hierarchy.Entries, entry => entry.EntryId == "alpha");
            Assert.Contains(hierarchy.Entries, entry => entry.EntryId == "beta");
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "litsearch_store_" + Guid.NewGuid().ToString("N"));
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
                    // ignore cleanup failures
                }
            }
        }
    }
}
