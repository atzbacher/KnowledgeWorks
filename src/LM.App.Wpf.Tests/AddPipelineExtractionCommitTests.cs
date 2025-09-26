using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Core.Utils;
using LM.Infrastructure.FileSystem;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Entries;
using LM.Infrastructure.Storage;
using LM.Infrastructure.Utils;
using Xunit;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.Tests
{
    public sealed class AddPipelineExtractionCommitTests : IDisposable
    {
        private readonly TempDir _temp;
        private readonly WorkspaceService _workspace;
        private readonly JsonEntryStore _entryStore;
        private readonly FileStorageService _storage;
        private readonly HashingService _hasher;
        private readonly HookOrchestrator _orchestrator;
        private readonly AddPipeline _pipeline;

        public AddPipelineExtractionCommitTests()
        {
            _temp = new TempDir();
            _workspace = new WorkspaceService();
            _workspace.EnsureWorkspaceAsync(_temp.Path).GetAwaiter().GetResult();

            _entryStore = new JsonEntryStore(_workspace);
            _entryStore.InitializeAsync().GetAwaiter().GetResult();
            _storage = new FileStorageService(_workspace);
            _hasher = new HashingService();
            _orchestrator = new HookOrchestrator(_workspace);

            var similarity = new NullSimilarityService();
            var metadata = new StubMetadataExtractor();

            _pipeline = new AddPipeline(
                _entryStore,
                _storage,
                _hasher,
                similarity,
                _workspace,
                metadata,
                NullPublicationLookup.Instance,
                NullDoiNormalizer.Instance,
                _orchestrator,
                new NullPmidNormalizer(),
                NullDataExtractionPreprocessor.Instance);
        }

        [Fact]
        public async Task CommitAsync_WithExtraction_PersistsExtractionAndLogs()
        {
            var file = _temp.CreateFile("evidence.pdf", "sample content");
            var staging = CreateStagingItem(file);

            await _pipeline.CommitAsync(new[] { staging }, CancellationToken.None);

            var sha = await _hasher.ComputeSha256Async(file, CancellationToken.None);
            var entry = await _entryStore.FindByHashAsync(sha, CancellationToken.None);
            Assert.NotNull(entry);
            var entryId = entry!.Id;
            Assert.False(string.IsNullOrWhiteSpace(entryId));

            var hubPath = Path.Combine(_workspace.GetWorkspaceRoot(), "entries", entryId, "hub.json");
            Assert.True(File.Exists(hubPath));
            var hubJson = await File.ReadAllTextAsync(hubPath);
            using var doc = JsonDocument.Parse(hubJson);
            var hooksNode = doc.RootElement.GetProperty("hooks");
            Assert.True(hooksNode.TryGetProperty("data_extraction", out var pointer));
            var relativeExtraction = pointer.GetString();
            Assert.False(string.IsNullOrWhiteSpace(relativeExtraction));
            var extractionAbsolute = _workspace.GetAbsolutePath(relativeExtraction!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(extractionAbsolute));

            var changeLogPath = Path.Combine(_workspace.GetWorkspaceRoot(), "entries", entryId, "hooks", "changelog.json");
            Assert.True(File.Exists(changeLogPath));
            var changeLog = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(await File.ReadAllTextAsync(changeLogPath), HookM.JsonStd.Options);
            Assert.NotNull(changeLog);
            var extractionEvent = changeLog!.Events.Last(evt => evt.Action == "DataExtractionCommitted");
            Assert.Equal(GetCurrentUserName(), extractionEvent.PerformedBy);
            Assert.Contains($"asset:article:sha256-{sha}", extractionEvent.Details!.Tags);
            var expectedExtractionHash = ComputeExtractionHash(staging.DataExtractionHook!);
            Assert.Contains($"asset:data-extraction:{expectedExtractionHash}", extractionEvent.Details!.Tags);
        }

        [Fact]
        public async Task CommitAsync_MetadataOnly_SkipsExtractionButLogs()
        {
            var file = _temp.CreateFile("metadata.pdf", "metadata only");
            var staging = CreateStagingItem(file);
            staging.CommitMetadataOnly = true;

            await _pipeline.CommitAsync(new[] { staging }, CancellationToken.None);

            var sha = await _hasher.ComputeSha256Async(file, CancellationToken.None);
            var entry = await _entryStore.FindByHashAsync(sha, CancellationToken.None);
            Assert.NotNull(entry);
            var entryId = entry!.Id;

            var hubPath = Path.Combine(_workspace.GetWorkspaceRoot(), "entries", entryId, "hub.json");
            var hubJson = await File.ReadAllTextAsync(hubPath);
            using var doc = JsonDocument.Parse(hubJson);
            var hooksNode = doc.RootElement.GetProperty("hooks");
            Assert.False(hooksNode.TryGetProperty("data_extraction", out _));

            var changeLogPath = Path.Combine(_workspace.GetWorkspaceRoot(), "entries", entryId, "hooks", "changelog.json");
            var changeLog = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(await File.ReadAllTextAsync(changeLogPath), HookM.JsonStd.Options);
            Assert.NotNull(changeLog);
            var extractionEvent = changeLog!.Events.Last(evt => evt.Action == "DataExtractionSkipped");
            Assert.Equal(GetCurrentUserName(), extractionEvent.PerformedBy);
            Assert.Contains($"asset:article:sha256-{sha}", extractionEvent.Details!.Tags);
            Assert.Contains("asset:data-extraction:none", extractionEvent.Details!.Tags);
        }

        public void Dispose()
        {
            _temp.Dispose();
        }

        private static string ComputeExtractionHash(HookM.DataExtractionHook hook)
        {
            var json = JsonSerializer.Serialize(hook, HookM.JsonStd.Options);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha.ComputeHash(bytes);
            return $"sha256-{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        private static string GetCurrentUserName()
            => SystemUser.GetCurrent();

        private static HookM.DataExtractionHook CreateExtractionHook()
        {
            var population = new HookM.DataExtractionPopulation
            {
                Id = "pop-1",
                Label = "Adults",
                SampleSize = 42
            };

            var intervention = new HookM.DataExtractionIntervention
            {
                Id = "arm-1",
                Name = "Drug A",
                PopulationIds = { "pop-1" },
                Dosage = "10mg"
            };

            var endpoint = new HookM.DataExtractionEndpoint
            {
                Id = "end-1",
                Name = "Mortality",
                PopulationIds = { "pop-1" },
                InterventionIds = { "arm-1" },
                Confirmed = true
            };

            var table = new HookM.DataExtractionTable
            {
                Id = "tbl-1",
                Title = "Primary Outcome",
                Caption = "Efficacy",
                SourcePath = "staging/manual/tables/tbl-1.csv",
                ProvenanceHash = "sha256-abcd",
                Pages = { "1" }
            };

            return new HookM.DataExtractionHook
            {
                ExtractedBy = GetCurrentUserName(),
                ExtractedAtUtc = DateTime.UtcNow,
                Populations = { population },
                Interventions = { intervention },
                Endpoints = { endpoint },
                Tables = { table }
            };
        }

        private static StagingItem CreateStagingItem(string filePath)
        {
            return new StagingItem
            {
                Selected = true,
                FilePath = filePath,
                SuggestedAction = "New",
                Type = EntryType.Publication,
                Title = "Sample Trial",
                DisplayName = "Sample Trial",
                AuthorsCsv = "Doe",
                Year = 2024,
                Source = "Journal of Tests",
                TagsCsv = "evidence",
                DataExtractionHook = CreateExtractionHook()
            };
        }

        private sealed class NullSimilarityService : ISimilarityService
        {
            public Task<double> ComputeFileSimilarityAsync(string filePathA, string filePathB, CancellationToken ct = default)
                => Task.FromResult(0d);
        }

        private sealed class StubMetadataExtractor : IMetadataExtractor
        {
            public Task<FileMetadata> ExtractAsync(string absolutePath, CancellationToken ct = default)
                => Task.FromResult(new FileMetadata());
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_pipeline_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string CreateFile(string name, string contents)
            {
                var full = System.IO.Path.Combine(Path, name);
                File.WriteAllText(full, contents);
                return full;
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
