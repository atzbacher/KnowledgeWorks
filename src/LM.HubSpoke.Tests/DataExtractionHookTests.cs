using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.HubSpoke.FileSystem;
using LM.HubSpoke.Hubs;
using LM.HubSpoke.Models;
using Xunit;

namespace LM.HubSpoke.Tests
{
    public sealed class DataExtractionHookTests
    {
        [Fact]
        public async Task HubJsonStore_RoundTripsExtractionPointer()
        {
            using var workspace = new TempWorkspace();
            var ws = new TestWorkspaceService(workspace.RootPath);
            WorkspaceLayout.Ensure(ws);

            var now = new DateTime(2024, 04, 01, 8, 30, 0, DateTimeKind.Utc);
            var hook = new DataExtractionHook
            {
                ExtractedBy = "CONTOSO\\tester",
                ExtractedAtUtc = now,
                Populations =
                {
                    new DataExtractionPopulation
                    {
                        Id = "pop-1",
                        Label = "Adults",
                        SampleSize = 10
                    }
                },
                Figures =
                {
                    new DataExtractionFigure
                    {
                        Id = "fig-1",
                        Title = "Flow diagram",
                        FigureLabel = "Figure 1",
                        ProvenanceHash = "sha256-deadbeef"
                    }
                }
            };

            var json = JsonSerializer.Serialize(hook, new JsonSerializerOptions { WriteIndented = true });
            var hash = ComputeHash(json);
            var relative = WorkspaceLayout.DataExtractionRelativePath(hash).Replace(Path.DirectorySeparatorChar, '/');
            var absolute = ws.GetAbsolutePath(relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, json);

            var entryId = "ex123";
            var hub = new EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Extraction",
                CreatedUtc = now,
                UpdatedUtc = now,
                CreatedBy = new PersonRef("tester", "tester") { TimestampUtc = now },
                UpdatedBy = new PersonRef("tester", "tester") { TimestampUtc = now },
                Hooks = new EntryHooks
                {
                    Article = "hooks/article.json",
                    DataExtraction = relative
                }
            };

            await HubJsonStore.SaveAsync(ws, hub, CancellationToken.None);
            var reloaded = await HubJsonStore.LoadAsync(ws, entryId, CancellationToken.None);

            Assert.NotNull(reloaded);
            Assert.Equal(relative, reloaded!.Hooks.DataExtraction);

            var stored = JsonSerializer.Deserialize<DataExtractionHook>(await File.ReadAllTextAsync(absolute));
            Assert.NotNull(stored);
            Assert.Equal("Adults", stored!.Populations[0].Label);
            Assert.Equal("sha256-deadbeef", stored.Figures[0].ProvenanceHash);
        }

        private static string ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(payload);
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private sealed class TempWorkspace : IDisposable
        {
            public string RootPath { get; }

            public TempWorkspace()
            {
                RootPath = Path.Combine(Path.GetTempPath(), "lm_extraction_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public void Dispose()
            {
                try { Directory.Delete(RootPath, recursive: true); } catch { /* ignore */ }
            }
        }

        private sealed class TestWorkspaceService : IWorkSpaceService
        {
            public string? WorkspacePath { get; }

            public TestWorkspaceService(string rootPath)
            {
                WorkspacePath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            }

            public string GetWorkspaceRoot() => WorkspacePath!;

            public string GetLocalDbPath() => Path.Combine(WorkspacePath!, "db.sqlite");

            public string GetAbsolutePath(string relativePath)
            {
                relativePath ??= string.Empty;
                relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(WorkspacePath!, relativePath);
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
                => Task.CompletedTask;
        }
    }
}
