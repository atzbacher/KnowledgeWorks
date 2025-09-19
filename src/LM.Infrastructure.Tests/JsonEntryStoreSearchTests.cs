using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Infrastructure.Entries;
using LM.Infrastructure.FileSystem;
using Xunit;

namespace LM.Infrastructure.Tests.Entries
{
    public sealed class JsonEntryStoreSearchTests
    {
        [Fact]
        public async Task SearchAsync_FiltersByMetadataFields()
        {
            using var temp = new TempWorkspace();
            var store = await CreateStoreAsync(temp.Path);

            var included = new Entry
            {
                Id = "included",
                Title = "Alpha",
                Source = "Science Journal",
                InternalId = "KW-001",
                Doi = "10.1000/alpha",
                Pmid = "12345678",
                Nct = "NCT0001",
                AddedBy = "alice",
                AddedOnUtc = new DateTime(2024, 1, 10, 12, 30, 0, DateTimeKind.Utc),
                Type = EntryType.Publication,
                Year = 2024,
                Tags = new List<string> { "immunology" }
            };

            var excluded = new Entry
            {
                Id = "excluded",
                Title = "Beta",
                Source = "Other Source",
                InternalId = "KW-999",
                Doi = "10.9999/beta",
                Pmid = "00000000",
                Nct = "NCT0999",
                AddedBy = "bob",
                AddedOnUtc = new DateTime(2023, 12, 1, 8, 0, 0, DateTimeKind.Utc),
                Type = EntryType.Report,
                Year = 2023,
                Tags = new List<string> { "oncology" }
            };

            await store.SaveAsync(included);
            await store.SaveAsync(excluded);

            var filter = new EntryFilter
            {
                SourceContains = "science",
                InternalIdContains = "001",
                DoiContains = "ALPHA",
                PmidContains = "3456",
                NctContains = "0001",
                AddedByContains = "ali",
                AddedOnFromUtc = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                AddedOnToUtc = new DateTime(2024, 1, 10, 23, 59, 59, DateTimeKind.Utc)
            };

            var results = await store.SearchAsync(filter);

            var entry = Assert.Single(results);
            Assert.Equal("included", entry.Id);
        }

        [Fact]
        public async Task SearchAsync_OrdersBySourceThenAddedOnAndId()
        {
            using var temp = new TempWorkspace();
            var store = await CreateStoreAsync(temp.Path);

            var earlyAlpha = new Entry
            {
                Id = "alpha-early",
                Title = "Shared",
                Source = "Alpha Journal",
                AddedOnUtc = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                Type = EntryType.Publication,
                Year = 2024
            };

            var lateAlpha = new Entry
            {
                Id = "alpha-late",
                Title = "Shared",
                Source = "Alpha Journal",
                AddedOnUtc = new DateTime(2024, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                Type = EntryType.Publication,
                Year = 2024
            };

            var beta = new Entry
            {
                Id = "beta",
                Title = "Shared",
                Source = "Beta Journal",
                AddedOnUtc = new DateTime(2024, 1, 3, 8, 0, 0, DateTimeKind.Utc),
                Type = EntryType.Publication,
                Year = 2024
            };

            await store.SaveAsync(beta);
            await store.SaveAsync(lateAlpha);
            await store.SaveAsync(earlyAlpha);

            var results = await store.SearchAsync(new EntryFilter());

            Assert.Equal(new[] { "alpha-early", "alpha-late", "beta" }, results.Select(r => r.Id).ToArray());
        }

        private static async Task<JsonEntryStore> CreateStoreAsync(string workspacePath)
        {
            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(workspacePath);
            var store = new JsonEntryStore(ws);
            await store.InitializeAsync();
            return store;
        }

        private sealed class TempWorkspace : IDisposable
        {
            public string Path { get; }

            public TempWorkspace()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_jsonstore_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
