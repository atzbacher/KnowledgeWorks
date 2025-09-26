using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.FileSystem;   // WorkspaceService
using LM.HubSpoke.FileSystem;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Tests.Hooks
{
    public sealed class HookOrchestratorTests
    {
        [Fact]
        public async Task ProcessAsync_WithArticle_WritesArticleJson()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var orch = new HookOrchestrator(ws);

            var ctx = new HookContext
            {
                Article = new HookM.ArticleHook
                {
                    Abstract = new HookM.ArticleAbstract
                    {
                        Sections = { new HookM.AbstractSection { Label = "Intro", Text = "Hello" } },
                        Text = "Plain"
                    }
                }
            };

            await orch.ProcessAsync("id123", ctx, CancellationToken.None);

            var path = Path.Combine(temp.Path, "entries", "id123", "hooks", "article.json");
            Assert.True(File.Exists(path), $"Expected: {path}");

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var abs = doc.RootElement.GetProperty("abstract");
            Assert.Equal("Plain", abs.GetProperty("text").GetString());
            Assert.Equal("Hello", abs.GetProperty("sections")[0].GetProperty("content").GetString());
        }

        [Fact]
        public async Task ProcessAsync_WithAttachments_WritesAttachmentsJson()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var orch = new HookOrchestrator(ws);

            var ctx = new HookContext
            {
                Attachments = new HookM.AttachmentHook
                {
                    Attachments = new List<HookM.AttachmentHookItem>
                    {
                        new HookM.AttachmentHookItem
                        {
                            AttachmentId = "att-1",
                            Title = "supplement",
                            LibraryPath = "library/aa/bb/file.pdf",
                            Tags = new List<string> { "data" },
                            Purpose = AttachmentKind.Supplement,
                            AddedBy = "tester",
                            AddedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        }
                    }
                }
            };

            await orch.ProcessAsync("id123", ctx, CancellationToken.None);

            var path = Path.Combine(temp.Path, "entries", "id123", "hooks", "attachments.json");
            Assert.True(File.Exists(path), $"Expected attachments hook at {path}");

            var hook = JsonSerializer.Deserialize<HookM.AttachmentHook>(await File.ReadAllTextAsync(path));
            Assert.NotNull(hook);
            Assert.Single(hook!.Attachments);
            Assert.Equal("supplement", hook.Attachments[0].Title);
        }

        [Fact]
        public async Task ProcessAsync_WithChangeLog_AppendsEvents()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var orch = new HookOrchestrator(ws);

            var initial = new HookContext
            {
                ChangeLog = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new HookM.EntryChangeLogEvent
                        {
                            EventId = "evt-1",
                            TimestampUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                            PerformedBy = "tester",
                            Action = "AttachmentAdded",
                            Details = new HookM.ChangeLogAttachmentDetails
                            {
                                AttachmentId = "att-1",
                                Title = "supplement",
                                LibraryPath = "library/aa/bb/file.pdf",
                                Purpose = AttachmentKind.Supplement,
                                Tags = new List<string> { "data" }
                            }
                        }
                    }
                }
            };

            await orch.ProcessAsync("id123", initial, CancellationToken.None);

            var path = Path.Combine(temp.Path, "entries", "id123", "hooks", "changelog.json");
            Assert.True(File.Exists(path));

            var hook = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(await File.ReadAllTextAsync(path));
            Assert.NotNull(hook);
            Assert.Single(hook!.Events);
            Assert.Equal("tester", hook.Events[0].PerformedBy);

            var append = new HookContext
            {
                ChangeLog = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new HookM.EntryChangeLogEvent
                        {
                            EventId = "evt-2",
                            TimestampUtc = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                            PerformedBy = "tester2",
                            Action = "AttachmentAdded",
                            Details = new HookM.ChangeLogAttachmentDetails
                            {
                                AttachmentId = "att-2",
                                Title = "slides",
                                LibraryPath = "library/cc/dd/file.pdf",
                                Purpose = AttachmentKind.Presentation,
                                Tags = new List<string>()
                            }
                        }
                    }
                }
            };

            await orch.ProcessAsync("id123", append, CancellationToken.None);

            hook = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(await File.ReadAllTextAsync(path));
            Assert.NotNull(hook);
            Assert.Equal(2, hook!.Events.Count);
            Assert.Equal("tester2", hook.Events[1].PerformedBy);
        }

        [Fact]
        public async Task ProcessAsync_WithDataExtraction_WritesHashTreeAndUpdatesHub()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var entryId = "ex123";
            var now = new DateTime(2024, 04, 01, 8, 30, 0, DateTimeKind.Utc);
            var hub = new HookM.EntryHub
            {
                EntryId = entryId,
                DisplayTitle = "Extraction",
                CreatedUtc = now,
                UpdatedUtc = now,
                CreatedBy = new HookM.PersonRef("tester", "tester") { TimestampUtc = now },
                UpdatedBy = new HookM.PersonRef("tester", "tester") { TimestampUtc = now },
                Hooks = new HookM.EntryHooks
                {
                    Article = "hooks/article.json"
                }
            };

            Directory.CreateDirectory(Path.Combine(temp.Path, "entries", entryId));
            await File.WriteAllTextAsync(
                WorkspaceLayout.HubPath(ws, entryId),
                JsonSerializer.Serialize(hub, HookM.JsonStd.Options));

            var orch = new HookOrchestrator(ws);

            var extractionHook = new HookM.DataExtractionHook
            {
                ExtractedBy = "CONTOSO\\tester",
                ExtractedAtUtc = now,
                Populations =
                {
                    new HookM.DataExtractionPopulation
                    {
                        Id = "pop-1",
                        Label = "Adults",
                        SampleSize = 42
                    }
                },
                Interventions =
                {
                    new HookM.DataExtractionIntervention
                    {
                        Id = "arm-1",
                        Name = "Drug A",
                        PopulationIds = { "pop-1" },
                        Dosage = "5mg"
                    }
                },
                Endpoints =
                {
                    new HookM.DataExtractionEndpoint
                    {
                        Id = "end-1",
                        Name = "Mortality",
                        PopulationIds = { "pop-1" },
                        InterventionIds = { "arm-1" },
                        ResultSummary = "No difference"
                    }
                },
                Tables =
                {
                    new HookM.DataExtractionTable
                    {
                        Id = "tbl-1",
                        Title = "Primary outcome",
                        TableLabel = "Table 1",
                        ProvenanceHash = "sha256-abcdef",
                        Summary = "Summary"
                    }
                }
            };

            var ctx = new HookContext
            {
                DataExtraction = extractionHook,
                ChangeLog = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new()
                        {
                            EventId = "evt-3",
                            TimestampUtc = now,
                            PerformedBy = "CONTOSO\\tester",
                            Action = "DataExtractionUpdated"
                        }
                    }
                }
            };

            await orch.ProcessAsync(entryId, ctx, CancellationToken.None);

            var serialized = JsonSerializer.Serialize(extractionHook, new JsonSerializerOptions { WriteIndented = true });
            var hash = ComputeHash(serialized);
            var relative = WorkspaceLayout.DataExtractionRelativePath(hash).Replace(Path.DirectorySeparatorChar, '/');
            var absolute = ws.GetAbsolutePath(relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(absolute), $"Expected extraction JSON at {absolute}");

            var stored = JsonSerializer.Deserialize<HookM.DataExtractionHook>(await File.ReadAllTextAsync(absolute));
            Assert.NotNull(stored);
            Assert.Equal("Adults", stored!.Populations[0].Label);
            Assert.Equal("Primary outcome", stored.Tables[0].Title);
            Assert.Equal("sha256-abcdef", stored.Tables[0].ProvenanceHash);

            var hubJson = await File.ReadAllTextAsync(WorkspaceLayout.HubPath(ws, entryId));
            using var doc = JsonDocument.Parse(hubJson);
            var pointer = doc.RootElement.GetProperty("hooks").GetProperty("data_extraction").GetString();
            Assert.Equal(relative, pointer);

            var changeLogPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "changelog.json");
            Assert.True(File.Exists(changeLogPath));
            var changeLog = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(await File.ReadAllTextAsync(changeLogPath));
            Assert.Equal("DataExtractionUpdated", changeLog!.Events[^1].Action);
        }

        private static string ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(payload);
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }
            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_orch_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }
            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
