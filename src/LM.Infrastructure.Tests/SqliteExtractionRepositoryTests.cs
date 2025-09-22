using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LM.Core.Models;
using LM.Infrastructure.Extraction;
using LM.Infrastructure.FileSystem;
using Xunit;

namespace LM.Infrastructure.Tests
{
    public sealed class SqliteExtractionRepositoryTests : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly WorkspaceService _workspace;
        private readonly SqliteExtractionRepository _repository;

        public SqliteExtractionRepositoryTests()
        {
            _workspaceRoot = Path.Combine(Path.GetTempPath(), "kw-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workspaceRoot);

            _workspace = new WorkspaceService();
            _workspace.EnsureWorkspaceAsync(_workspaceRoot).GetAwaiter().GetResult();
            _repository = new SqliteExtractionRepository(_workspace);
        }

        [Fact]
        public async Task UpsertAndGetAsync_Roundtrip()
        {
            var descriptor = CreateDescriptor();

            await _repository.UpsertAsync(descriptor);
            var fetched = await _repository.GetAsync(descriptor.RegionHash);

            Assert.NotNull(fetched);
            Assert.Equal(descriptor.RegionHash, fetched!.RegionHash);
            Assert.Equal(descriptor.EntryHubId, fetched.EntryHubId);
            Assert.Equal(descriptor.Bounds.Width, fetched.Bounds.Width);
            Assert.Contains("tag1", fetched.Tags);
        }

        [Fact]
        public async Task SearchAsync_FindsDescriptorByOcr()
        {
            var descriptor = CreateDescriptor();
            descriptor.OcrText = "alpha beta gamma";

            await _repository.UpsertAsync(descriptor);

            var hits = await _repository.SearchAsync("alpha", 5);

            Assert.Single(hits);
            Assert.Equal(descriptor.RegionHash, hits[0].RegionHash);
        }

        [Fact]
        public async Task DeleteAsync_RemovesArtifacts()
        {
            var descriptor = CreateDescriptor();

            await _repository.UpsertAsync(descriptor);

            CreateWorkspaceFile(descriptor.ImagePath!);
            CreateWorkspaceFile(descriptor.OcrTextPath!);
            CreateWorkspaceFile(descriptor.OfficePackagePath!);

            var descriptorJson = GetDescriptorJsonPath(descriptor.RegionHash);
            Assert.True(File.Exists(descriptorJson));

            await _repository.DeleteAsync(descriptor.RegionHash);

            Assert.Null(await _repository.GetAsync(descriptor.RegionHash));
            Assert.False(File.Exists(descriptorJson));
            Assert.False(File.Exists(_workspace.GetAbsolutePath(descriptor.ImagePath!)));
            Assert.False(File.Exists(_workspace.GetAbsolutePath(descriptor.OcrTextPath!)));
            Assert.False(File.Exists(_workspace.GetAbsolutePath(descriptor.OfficePackagePath!)));
        }

        [Fact]
        public async Task SaveSessionAsync_PersistsRecentSession()
        {
            var descriptor = CreateDescriptor();

            await _repository.UpsertAsync(descriptor);

            var result = new RegionExportResult
            {
                Descriptor = descriptor,
                ExporterId = descriptor.ExporterId ?? "test",
                ImagePath = descriptor.ImagePath ?? string.Empty,
                OcrTextPath = descriptor.OcrTextPath,
                OfficePackagePath = descriptor.OfficePackagePath,
                WasCached = true,
                Duration = TimeSpan.FromMilliseconds(150),
                CompletedUtc = DateTime.UtcNow
            };

            await _repository.SaveSessionAsync(result);

            var sessions = await _repository.GetRecentSessionsAsync(5);
            Assert.NotEmpty(sessions);
            Assert.Equal(descriptor.RegionHash, sessions[0].Descriptor.RegionHash);
            Assert.True(sessions[0].WasCached);
            Assert.Equal(TimeSpan.FromMilliseconds(150), sessions[0].Duration);

            var recentDescriptors = await _repository.GetRecentAsync(5);
            Assert.Contains(recentDescriptors, d => d.RegionHash == descriptor.RegionHash);
        }

        private RegionDescriptor CreateDescriptor()
        {
            var hash = Guid.NewGuid().ToString("N");
            var bucket = Bucket(hash);

            var descriptor = new RegionDescriptor
            {
                RegionHash = hash,
                EntryHubId = "entry-123",
                SourceRelativePath = "library/sample.pdf",
                SourceSha256 = Guid.NewGuid().ToString("N"),
                PageNumber = 1,
                Bounds = new RegionBounds { X = 10, Y = 20, Width = 300, Height = 150 },
                OcrText = "lorem ipsum",
                Notes = "note",
                Annotation = "annotation",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                LastExportStatus = RegionExportStatus.Completed,
                ImagePath = Path.Combine("extraction", bucket.Item1, bucket.Item2, hash + ".png"),
                OcrTextPath = Path.Combine("extraction", bucket.Item1, bucket.Item2, hash + ".txt"),
                OfficePackagePath = Path.Combine("extraction", bucket.Item1, bucket.Item2, hash + ".ppmx"),
                ExporterId = "exporter-test"
            };
            descriptor.Tags.AddRange(new[] { "tag1", "tag2" });
            descriptor.ExtraMetadata["foo"] = "bar";
            return descriptor;
        }

        private void CreateWorkspaceFile(string relativePath)
        {
            var absolute = _workspace.GetAbsolutePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "test");
        }

        private string GetDescriptorJsonPath(string regionHash)
        {
            var (first, second) = Bucket(regionHash);
            return Path.Combine(_workspace.GetWorkspaceRoot(), "extraction", first, second, regionHash.ToLowerInvariant() + ".json");
        }

        private static (string, string) Bucket(string regionHash)
        {
            var normalized = (regionHash ?? string.Empty).Trim().ToLowerInvariant();
            var first = normalized.Length >= 2 ? normalized.Substring(0, 2) : normalized.PadRight(2, '0');
            var second = normalized.Length >= 4 ? normalized.Substring(2, 2) : normalized.Length > 2 ? normalized.Substring(2).PadRight(2, '0') : "00";
            return (first, second);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_workspaceRoot))
                    Directory.Delete(_workspaceRoot, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    internal static class RegionDescriptorExtensions
    {
        public static void AddRange(this IList<string> list, IEnumerable<string> values)
        {
            foreach (var value in values)
                list.Add(value);
        }
    }
}
