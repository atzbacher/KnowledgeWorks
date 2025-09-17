using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Infrastructure.Entries;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Storage;
using LM.Infrastructure.Utils;
using LM.Infrastructure.Text;

public sealed class TagsPropagationE2ETests
{
    [Fact]
    public async Task PubMed_Keywords_Propagate_To_Staging_And_Entry()
    {
        // Arrange: empty workspace
        using var temp = new TempDir();
        var ws = new WorkspaceService();
        await ws.EnsureWorkspaceAsync(temp.Path);

        // Real infra services (fast, filesystem-only)
        var store = new JsonEntryStore(ws);
        await store.InitializeAsync(CancellationToken.None);

        var storage = new FileStorageService(ws);
        var hasher = new HashingService();
        var similarity = new SimilarityService();
        var doiNorm = new DoiNormalizer();
        var pmidNorm = new PmidNormalizer();
        var orchestrator = new HookOrchestrator(ws);

        // Fakes we control for deterministic behavior:
        // - Metadata says: "this PDF has DOI 10.9999/e2e"
        // - PubMed returns keywords: ["term1", "term2"]
        var metadata = new FakeMetadataExtractor(doi: "10.9999/e2e", title: "E2E Paper");
        var pubmed = new FakePubLookup(new[] { "term1", "term2" });

        var pipeline = new AddPipeline(
            store, storage, hasher, similarity, ws,
            metadata, pubmed, doiNorm, orchestrator, pmidNorm, simLog: null);

        // Create a dummy PDF (content irrelevant — we stub metadata)
        var pdfPath = Path.Combine(temp.Path, "paper.pdf");
        await File.WriteAllTextAsync(pdfPath, "dummy pdf content");

        // Act 1: Stage
        var staged = await pipeline.StagePathsAsync(new[] { pdfPath }, CancellationToken.None);
        var item = Assert.Single(staged);

        Assert.NotNull(item.ArticleHook);
        Assert.Equal("Paper from PubMed", item.ArticleHook!.Article.Title);
        Assert.Equal("10.9999/e2e", item.ArticleHook.Identifier.DOI);
        Assert.Contains("term1", item.ArticleHook.Keywords, StringComparer.OrdinalIgnoreCase);

        // Assert (staging): the PubMed keywords are merged into TagsCsv
        Assert.False(string.IsNullOrWhiteSpace(item.TagsCsv));
        var stagedTags = item.TagsCsv!
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        Assert.Contains("term1", stagedTags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("term2", stagedTags, StringComparer.OrdinalIgnoreCase);

        // Act 2: Commit
        await pipeline.CommitAsync(staged, CancellationToken.None);

        // Assert (entry): the persisted Entry has those tags
        var saved = await store.FindByIdsAsync("10.9999/e2e", null, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Contains("term1", saved!.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("term2", saved.Tags, StringComparer.OrdinalIgnoreCase);

        var hookPath = ws.GetAbsolutePath(Path.Combine("entries", saved.Id, "hooks", "article.json"));
        Assert.True(File.Exists(hookPath));

        var json = await File.ReadAllTextAsync(hookPath);
        using var doc = JsonDocument.Parse(json);
        var hookKeywords = doc.RootElement.GetProperty("keywords")
            .EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        Assert.Contains("term1", hookKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("term2", hookKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Paper from PubMed", doc.RootElement.GetProperty("article").GetProperty("title").GetString());
        Assert.Equal("10.9999/e2e", doc.RootElement.GetProperty("identifier").GetProperty("doi").GetString());
    }

    // ---- Test fakes ---------------------------------------------------------

    private sealed class FakeMetadataExtractor : IMetadataExtractor
    {
        private readonly string _doi;
        private readonly string _title;
        public FakeMetadataExtractor(string doi, string title)
        {
            _doi = doi;
            _title = title;
        }

        public Task<FileMetadata> ExtractAsync(string absolutePath, CancellationToken ct = default)
            => Task.FromResult(new FileMetadata
            {
                Title = _title,
                Doi = _doi,
                // Leave Tags empty here to prove they come from PubMed keywords
                Tags = new System.Collections.Generic.List<string>()
            });
    }

    private sealed class FakePubLookup : IPublicationLookup
    {
        private readonly string[] _keywords;
        public FakePubLookup(string[] keywords) => _keywords = keywords;

        public Task<PublicationRecord?> TryGetByDoiAsync(string doi, bool includeCitedBy, CancellationToken ct)
        {
            var rec = new PublicationRecord
            {
                Doi = doi,
                Title = "Paper from PubMed",
                Year = 2021,
                Authors = new[]
                {
                    new AuthorName { Family = "Smith", Given = "Jane" },
                    new AuthorName { Family = "Doe",   Given = "John" }
                },
                Keywords = _keywords
            };
            return Task.FromResult<PublicationRecord?>(rec);
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_e2e_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
        }
    }
}
