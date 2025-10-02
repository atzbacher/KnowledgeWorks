using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Library.Collections;
using LM.Core.Abstractions;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryCollectionStoreTests
    {
        [Fact]
        public async Task CreateFolderAsync_PersistsCollectionWithMetadata()
        {
            using var workspace = new TempWorkspace();
            var store = new LibraryCollectionStore(workspace);

            var folderId = await store.CreateFolderAsync(LibraryCollectionFolder.RootId, "Reading List", "tester", CancellationToken.None);

            var hierarchy = await store.GetHierarchyAsync(CancellationToken.None);
            var folder = hierarchy.Folders.Single(f => string.Equals(f.Id, folderId, StringComparison.Ordinal));

            Assert.Equal("Reading List", folder.Name);
            Assert.Equal("tester", folder.Metadata.CreatedBy);
            Assert.Equal("tester", folder.Metadata.ModifiedBy);

            var filePath = Path.Combine(workspace.GetWorkspaceRoot(), "library", "collections.json");
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task AddEntriesAsync_AppendsEntriesWithCreator()
        {
            using var workspace = new TempWorkspace();
            var store = new LibraryCollectionStore(workspace);

            var folderId = await store.CreateFolderAsync(LibraryCollectionFolder.RootId, "Queue", "tester", CancellationToken.None);

            await store.AddEntriesAsync(folderId, new[] { "entry-1", "entry-2", "entry-1" }, "tester", CancellationToken.None);

            var hierarchy = await store.GetHierarchyAsync(CancellationToken.None);
            var folder = hierarchy.Folders.Single(f => string.Equals(f.Id, folderId, StringComparison.Ordinal));

            Assert.Equal(2, folder.Entries.Count);
            Assert.All(folder.Entries, entry => Assert.Equal("tester", entry.AddedBy));
        }

        [Fact]
        public async Task RemoveEntriesAsync_RemovesExistingEntries()
        {
            using var workspace = new TempWorkspace();
            var store = new LibraryCollectionStore(workspace);

            var folderId = await store.CreateFolderAsync(LibraryCollectionFolder.RootId, "Archive", "tester", CancellationToken.None);
            await store.AddEntriesAsync(folderId, new[] { "entry-1", "entry-2" }, "tester", CancellationToken.None);

            await store.RemoveEntriesAsync(folderId, new[] { "entry-1" }, "tester", CancellationToken.None);

            var hierarchy = await store.GetHierarchyAsync(CancellationToken.None);
            var folder = hierarchy.Folders.Single(f => string.Equals(f.Id, folderId, StringComparison.Ordinal));

            Assert.Single(folder.Entries);
            Assert.Equal("entry-2", folder.Entries[0].EntryId);
        }

        private sealed class TempWorkspace : IWorkSpaceService, IDisposable
        {
            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "kw-collection-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.Combine(RootPath, "library"));
            }

            public string RootPath { get; }

            public string? WorkspacePath => RootPath;

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                return Task.CompletedTask;
            }

            public string GetWorkspaceRoot() => RootPath;

            public string GetLocalDbPath() => Path.Combine(RootPath, "metadata.db");

            public string GetAbsolutePath(string relativePath) => Path.Combine(RootPath, relativePath ?? string.Empty);

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath))
                    {
                        Directory.Delete(RootPath, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup failures for tests
                }
            }
        }
    }
}
