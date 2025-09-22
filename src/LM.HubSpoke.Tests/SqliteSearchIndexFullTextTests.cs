using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Search;
using LM.HubSpoke.Indexing;
using Xunit;

namespace LM.HubSpoke.Tests
{
    public sealed class SqliteSearchIndexFullTextTests
    {
        [Fact]
        public async Task FullTextSearch_ReturnsMatchesWithScoreAndSnippet()
        {
            using var temp = new TempWorkspace();
            var ws = new TestWorkspaceService(temp.RootPath, temp.DbPath);
            var index = new SqliteSearchIndex(ws);
            await index.InitializeAsync();
            await index.ClearAsync();

            await index.UpsertAsync(CreateRecord(
                id: "e1",
                title: "AI biomarkers",
                abstractText: "The biomarker analysis shows progress.",
                content: "Detailed biomarker discussion.",
                year: 2024));

            await index.UpsertAsync(CreateRecord(
                id: "e2",
                title: "Unrelated study",
                abstractText: "Nothing to see here.",
                content: "General discussion.",
                year: 2023));

            var query = new FullTextSearchQuery
            {
                Text = "biomarker",
                Fields = FullTextSearchField.Abstract | FullTextSearchField.Content
            };

            var hits = await index.SearchAsync(query);

            Assert.Single(hits);
            var hit = hits[0];
            Assert.Equal("e1", hit.EntryId);
            Assert.True(hit.Score > 0 && hit.Score <= 1);
            Assert.NotNull(hit.Highlight);
            Assert.Contains("[biomarker", hit.Highlight, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task FullTextSearch_RespectsFieldScope()
        {
            using var temp = new TempWorkspace();
            var ws = new TestWorkspaceService(temp.RootPath, temp.DbPath);
            var index = new SqliteSearchIndex(ws);
            await index.InitializeAsync();
            await index.ClearAsync();

            await index.UpsertAsync(CreateRecord(
                id: "e1",
                title: "AI biomarkers",
                abstractText: "Signal processing for biomarkers.",
                content: "Biomarker rich content.",
                year: 2024));

            var titleOnlyQuery = new FullTextSearchQuery
            {
                Text = "biomarker",
                Fields = FullTextSearchField.Title
            };

            var hits = await index.SearchAsync(titleOnlyQuery);

            // Title has the word, so we should see it when scoped to title.
            Assert.Single(hits);

            var contentOnlyQuery = new FullTextSearchQuery
            {
                Text = "biomarker",
                Fields = FullTextSearchField.Content,
                YearFrom = 2025 // out of range
            };

            var contentHits = await index.SearchAsync(contentOnlyQuery);

            Assert.Empty(contentHits);
        }

        private static SqliteSearchIndex.IndexRecord CreateRecord(
            string id,
            string title,
            string abstractText,
            string content,
            int year)
            => new(
                EntryId: id,
                DisplayTitle: title,
                Year: year,
                IsInternal: false,
                Type: EntryType.Publication.ToString(),
                Doi: null,
                Pmid: null,
                Journal: "Science",
                Title: title,
                Abstract: abstractText,
                Authors: Array.Empty<string>(),
                Keywords: Array.Empty<string>(),
                Tags: Array.Empty<string>(),
                AssetHashes: Array.Empty<string>(),
                Content: content);

        private sealed class TestWorkspaceService : IWorkSpaceService
        {
            public TestWorkspaceService(string rootPath, string dbPath)
            {
                WorkspacePath = rootPath;
                DbPath = dbPath;
                Directory.CreateDirectory(rootPath);
            }

            public string? WorkspacePath { get; private set; }

            public string DbPath { get; }

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

            public string GetLocalDbPath() => DbPath;

            public string GetWorkspaceRoot() => WorkspacePath ?? throw new InvalidOperationException("WorkspacePath not set");
        }

        private sealed class TempWorkspace : IDisposable
        {
            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "kw-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
                DbPath = Path.Combine(RootPath, "metadata.db");
            }

            public string RootPath { get; }

            public string DbPath { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath))
                        Directory.Delete(RootPath, true);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
    }
}
