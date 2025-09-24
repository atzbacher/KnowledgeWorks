using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Search;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.Core.Models.Search;
using LM.Infrastructure.Search;
using Xunit;

namespace LM.Infrastructure.Tests.Search
{
    public sealed class SearchExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_FlagsHitWhenIdsMatch()
        {
            var hit = new SearchHit
            {
                Source = SearchDatabase.PubMed,
                ExternalId = "987654",
                Doi = "10.1234/sample",
                Title = "Sample Article",
                Year = 2024
            };

            var provider = new StubSearchProvider(SearchDatabase.PubMed, new[] { hit });
            var store = new StubEntryStore
            {
                FindByIdsAsyncImpl = (doi, pmid, ct) =>
                {
                    Assert.Equal("10.1234/sample", doi);
                    Assert.Equal("987654", pmid);
                    return Task.FromResult<Entry?>(new Entry { Id = "entry-1" });
                }
            };

            var service = new SearchExecutionService(new[] { provider }, store);

            var result = await service.ExecuteAsync(new SearchExecutionRequest
            {
                Database = SearchDatabase.PubMed,
                Query = "sample"
            });

            var resolved = Assert.Single(result.Hits);
            Assert.True(resolved.AlreadyInDb);
            Assert.Equal("entry-1", resolved.ExistingEntryId);
            Assert.True(store.FindByIdsCalled);
            Assert.False(store.FindSimilarCalled);
            Assert.False(store.EnumerateCalled);
        }

        [Fact]
        public async Task ExecuteAsync_FallsBackToTitleMatchWhenIdsMissing()
        {
            var hit = new SearchHit
            {
                Source = SearchDatabase.ClinicalTrialsGov,
                Title = "Fallback Study",
                Year = 2021
            };

            var provider = new StubSearchProvider(SearchDatabase.ClinicalTrialsGov, new[] { hit });
            var store = new StubEntryStore
            {
                FindByIdsAsyncImpl = (doi, pmid, ct) => throw new InvalidOperationException("FindByIdsAsync should not be called for fallback."),
                FindSimilarByNameYearAsyncImpl = (title, year, ct) =>
                {
                    Assert.Equal("Fallback Study", title);
                    Assert.Equal(2021, year);
                    IReadOnlyList<Entry> matches = new[]
                    {
                        new Entry { Id = "entry-2", Title = "Fallback Study", Year = 2021 }
                    };
                    return Task.FromResult(matches);
                }
            };

            var service = new SearchExecutionService(new[] { provider }, store);

            var result = await service.ExecuteAsync(new SearchExecutionRequest
            {
                Database = SearchDatabase.ClinicalTrialsGov,
                Query = "fallback"
            });

            var resolved = Assert.Single(result.Hits);
            Assert.True(resolved.AlreadyInDb);
            Assert.Equal("entry-2", resolved.ExistingEntryId);
            Assert.False(store.FindByIdsCalled);
            Assert.True(store.FindSimilarCalled);
            Assert.False(store.EnumerateCalled);
        }

        private sealed class StubSearchProvider : ISearchProvider
        {
            private readonly IReadOnlyList<SearchHit> _hits;

            public StubSearchProvider(SearchDatabase database, IReadOnlyList<SearchHit> hits)
            {
                Database = database;
                _hits = hits;
            }

            public SearchDatabase Database { get; }

            public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, DateTime? from, DateTime? to, CancellationToken ct = default)
                => Task.FromResult(_hits);
        }

        private sealed class StubEntryStore : IEntryStore
        {
            public bool EnumerateCalled { get; private set; }
            public bool FindByIdsCalled { get; private set; }
            public bool FindSimilarCalled { get; private set; }

            public Func<Entry, CancellationToken, Task>? SaveAsyncImpl { get; init; }
            public Func<string, CancellationToken, Task<Entry?>>? GetByIdAsyncImpl { get; init; }
            public Func<EntryFilter, CancellationToken, Task<IReadOnlyList<Entry>>>? SearchAsyncImpl { get; init; }
            public Func<string, CancellationToken, Task<Entry?>>? FindByHashAsyncImpl { get; init; }
            public Func<string?, string?, CancellationToken, Task<Entry?>> FindByIdsAsyncImpl { get; init; }
                = (_, _, _) => Task.FromResult<Entry?>(null);
            public Func<string, int?, CancellationToken, Task<IReadOnlyList<Entry>>> FindSimilarByNameYearAsyncImpl { get; init; }
                = (_, _, _) => Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default)
                => SaveAsyncImpl?.Invoke(entry, ct) ?? Task.CompletedTask;

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
                => GetByIdAsyncImpl?.Invoke(id, ct) ?? Task.FromResult<Entry?>(null);

            public IAsyncEnumerable<Entry> EnumerateAsync(CancellationToken ct = default)
            {
                EnumerateCalled = true;
                return ThrowEnumeration();

                static async IAsyncEnumerable<Entry> ThrowEnumeration()
                {
                    await Task.Yield();
                    throw new InvalidOperationException("EnumerateAsync should not be called.");
#pragma warning disable CS0162
                    yield break;
#pragma warning restore CS0162
                }
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
                => SearchAsyncImpl?.Invoke(filter, ct) ?? Task.FromResult<IReadOnlyList<Entry>>(Array.Empty<Entry>());

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
                => FindByHashAsyncImpl?.Invoke(sha256, ct) ?? Task.FromResult<Entry?>(null);

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
            {
                FindByIdsCalled = true;
                return FindByIdsAsyncImpl(doi, pmid, ct);
            }

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
            {
                FindSimilarCalled = true;
                return FindSimilarByNameYearAsyncImpl(title, year, ct);
            }
        }
    }
}
