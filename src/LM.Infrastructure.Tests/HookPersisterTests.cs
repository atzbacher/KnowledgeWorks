using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.FileSystem;
using HookM = LM.HubSpoke.Models;

public class HookPersisterTests
{
    [Fact]
    public async Task Persister_WritesArticleJson()
    {
        using var temp = new TempDir();
        var ws = new WorkspaceService();
        await ws.EnsureWorkspaceAsync(temp.Path);

        var persister = new HookPersister(ws);
        var hook = new HookM.ArticleHook
        {
            Abstract = new HookM.ArticleAbstract
            {
                Sections = { new HookM.AbstractSection { Label = "Intro", Text = "Hello" } },
                Text = "Plain"
            }
        };

        await persister.SaveArticleIfAnyAsync("id123", hook, CancellationToken.None);

        var file = Path.Combine(temp.Path, "entries", "id123", "hooks", "article.json");
        Assert.True(File.Exists(file));

        var json = await File.ReadAllTextAsync(file);
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_hooks_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
