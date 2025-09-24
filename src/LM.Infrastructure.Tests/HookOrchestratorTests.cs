using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using LM.Core.Models;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.FileSystem;   // WorkspaceService
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
