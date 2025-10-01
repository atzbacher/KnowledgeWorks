using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Models.Filters;
using LM.HubSpoke.Models;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Pdf;
using Xunit;

namespace LM.Infrastructure.Tests.Pdf
{
    public sealed class PdfAnnotationOverlayReaderTests
    {
        [Fact]
        public async Task GetOverlayJsonAsync_ReturnsOverlayContent()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var entryStore = new FakeEntryStore();
            const string entryId = "overlay-entry";
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            entryStore.Add(new Entry
            {
                Id = entryId,
                MainFileHashSha256 = hash
            });

            var reader = new PdfAnnotationOverlayReader(workspace, entryStore);
            var overlayRelative = $"library/{hash[..2]}/{hash}/{hash}.json";
            var overlayAbsolute = Path.Combine(temp.Path, overlayRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(overlayAbsolute)!);
            const string overlayPayload = "{\"foo\":\"bar\"}";
            await File.WriteAllTextAsync(overlayAbsolute, overlayPayload);

            var hookDirectory = Path.Combine(temp.Path, "entries", entryId, "hooks");
            Directory.CreateDirectory(hookDirectory);
            var hook = new PdfAnnotationsHook
            {
                OverlayPath = overlayRelative
            };
            var hookPath = Path.Combine(hookDirectory, "pdf_annotations.json");
            await File.WriteAllTextAsync(hookPath, JsonSerializer.Serialize(hook, JsonStd.Options));

            var result = await reader.GetOverlayJsonAsync(hash, CancellationToken.None);

            Assert.Equal(overlayPayload, result);
        }

        [Fact]
        public async Task GetOverlayJsonAsync_ReturnsNullWhenHookMissing()
        {
            using var temp = new TempDir();

            var workspace = new WorkspaceService();
            await workspace.EnsureWorkspaceAsync(temp.Path);

            var entryStore = new FakeEntryStore();
            var reader = new PdfAnnotationOverlayReader(workspace, entryStore);
            const string hash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var result = await reader.GetOverlayJsonAsync(hash, CancellationToken.None);

            Assert.Null(result);
        }

        private sealed class FakeEntryStore : IEntryStore
        {
            private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, Entry> _byId = new(StringComparer.OrdinalIgnoreCase);

            public void Add(Entry entry)
            {
                if (entry is null)
                    throw new ArgumentNullException(nameof(entry));

                if (string.IsNullOrWhiteSpace(entry.Id))
                    throw new ArgumentException("Entry id must be provided", nameof(entry));
                if (string.IsNullOrWhiteSpace(entry.MainFileHashSha256))
                    throw new ArgumentException("Entry hash must be provided", nameof(entry));

                _byId[entry.Id] = entry;
                _entries[entry.MainFileHashSha256] = entry;
            }

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task SaveAsync(Entry entry, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
            {
                _byId.TryGetValue(id, out var entry);
                return Task.FromResult<Entry?>(entry);
            }

            public async IAsyncEnumerable<Entry> EnumerateAsync([EnumeratorCancellation] CancellationToken ct = default)
            {
                foreach (var entry in _byId.Values)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return entry;
                    await Task.Yield();
                }
            }

            public Task<IReadOnlyList<Entry>> SearchAsync(EntryFilter filter, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<Entry?> FindByHashAsync(string sha256, CancellationToken ct = default)
            {
                if (sha256 is null)
                {
                    return Task.FromResult<Entry?>(null);
                }

                _entries.TryGetValue(sha256, out var entry);
                return Task.FromResult<Entry?>(entry);
            }

            public Task<IReadOnlyList<Entry>> FindSimilarByNameYearAsync(string title, int? year, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<Entry?> FindByIdsAsync(string? doi, string? pmid, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "overlay_reader_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { }
            }
        }
    }
}
