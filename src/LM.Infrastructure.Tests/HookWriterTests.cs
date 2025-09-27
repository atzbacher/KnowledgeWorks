using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using LM.Infrastructure.Hooks;                 // HookWriter
using LM.Infrastructure.FileSystem;           // WorkspaceService
using HookM = LM.HubSpoke.Models;             // ArticleHook DTOs

namespace LM.Infrastructure.Tests.Hooks
{
    public sealed class HookWriterTests
    {
        [Fact]
        public async Task SaveArticleAsync_WritesJsonUnderEntriesHooks()
        {
            using var temp = new TempDir();

            // Use real workspace service to avoid fake/stub drift
            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var writer = new HookWriter(ws);

            var hook = new HookM.ArticleHook
            {
                Abstract = new HookM.ArticleAbstract
                {
                    Sections = { new HookM.AbstractSection { Label = "Intro", Text = "Hello" } },
                    Text = "Plain"
                }
            };

            var entryId = "abc123";
            await writer.SaveArticleAsync(entryId, hook, CancellationToken.None);

            var file = Path.Combine(temp.Path, "entries", entryId, "hooks", "article.json");
            Assert.True(File.Exists(file), $"Expected file at: {file}");

            var json = await File.ReadAllTextAsync(file);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // abstract object exists
            Assert.True(root.TryGetProperty("abstract", out var absEl),
                "Missing 'abstract' in hook JSON. Preview:\n" + Preview(json));

            // abstract.text exists and equals "Plain"
            Assert.True(absEl.TryGetProperty("text", out var absTextEl),
                "Missing 'abstract.text' in hook JSON. Preview:\n" + Preview(json));
            Assert.Equal("Plain", absTextEl.GetString());

            // abstract.sections[0].content == "Hello"
            Assert.True(absEl.TryGetProperty("sections", out var sectionsEl) &&
                        sectionsEl.ValueKind == JsonValueKind.Array &&
                        sectionsEl.GetArrayLength() > 0,
                "Missing/empty 'abstract.sections' array. Preview:\n" + Preview(json));

            var firstSection = sectionsEl[0];
            Assert.True(firstSection.TryGetProperty("content", out var contentEl),
                "Missing 'content' in first abstract section. Preview:\n" + Preview(json));
            Assert.Equal("Hello", contentEl.GetString());
        }

        [Fact]
        public async Task AppendChangeLogAsync_RetriesWhenFileIsLocked()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var writer = new HookWriter(ws);
            var entryId = "retry-entry";

            var initialHook = new HookM.EntryChangeLogHook
            {
                Events = new List<HookM.EntryChangeLogEvent>
                {
                    new() { Action = "created", PerformedBy = "tester", TimestampUtc = DateTime.UtcNow }
                }
            };

            await writer.AppendChangeLogAsync(entryId, initialHook, CancellationToken.None);

            var changeLogPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "changelog.json");

            using var externalHandle = new FileStream(
                changeLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var appendTask = writer.AppendChangeLogAsync(
                entryId,
                new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent>
                    {
                        new() { Action = "updated", PerformedBy = "tester", TimestampUtc = DateTime.UtcNow }
                    }
                },
                CancellationToken.None);

            await Task.Delay(200);

            if (OperatingSystem.IsWindows())
            {
                Assert.False(appendTask.IsCompleted, "Append should wait for the external handle to be released.");
            }

            externalHandle.Dispose();

            await appendTask;

            var json = await File.ReadAllTextAsync(changeLogPath);
            var payload = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(json);

            Assert.NotNull(payload);
            Assert.NotNull(payload!.Events);
            Assert.Equal(2, payload.Events!.Count);
        }

        [Fact]
        public async Task AppendChangeLogAsync_AppendsBackToBack()
        {
            using var temp = new TempDir();

            var ws = new WorkspaceService();
            await ws.EnsureWorkspaceAsync(temp.Path);

            var writer = new HookWriter(ws);
            var entryId = "double-append";

            var first = new HookM.EntryChangeLogHook
            {
                Events = new List<HookM.EntryChangeLogEvent>
                {
                    new() { Action = "created", PerformedBy = "tester", TimestampUtc = DateTime.UtcNow }
                }
            };

            await writer.AppendChangeLogAsync(entryId, first, CancellationToken.None);

            var second = new HookM.EntryChangeLogHook
            {
                Events = new List<HookM.EntryChangeLogEvent>
                {
                    new() { Action = "updated", PerformedBy = "tester", TimestampUtc = DateTime.UtcNow }
                }
            };

            await writer.AppendChangeLogAsync(entryId, second, CancellationToken.None);

            var changeLogPath = Path.Combine(temp.Path, "entries", entryId, "hooks", "changelog.json");
            var json = await File.ReadAllTextAsync(changeLogPath);
            var payload = JsonSerializer.Deserialize<HookM.EntryChangeLogHook>(json);

            Assert.NotNull(payload);
            Assert.NotNull(payload!.Events);
            Assert.Equal(2, payload.Events!.Count);
        }

        private static string Preview(string s)
            => s.Length > 600 ? s[..600] + "..." : s;

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }
            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_hooks_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }
            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
