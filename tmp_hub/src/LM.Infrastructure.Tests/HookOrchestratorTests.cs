using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
