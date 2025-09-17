using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LM.App.Wpf.Diagnostics;
using LM.App.Wpf.ViewModels;
using LM.HubSpoke.Models;
using LM.Infrastructure.FileSystem;
using Xunit;

public class StagingDebugDumperTests
{
    [Fact]
    public async Task Dump_Writes_Json_With_Tags_And_Spokes()
    {
        using var temp = new TempDir();
        var ws = new WorkspaceService();
        await ws.EnsureWorkspaceAsync(temp.Path);

        DebugFlags.DumpStagingJson = true;

        var staged = new StagingItem
        {
            FilePath = Path.Combine(temp.Path, "alpha.pdf"),
            Title = "Alpha",
            TagsCsv = "one, two",
            IsInternal = false,
            // Include an ArticleHook so we expect a spoke
            ArticleHook = new ArticleHook
            {
                Title = "Alpha",
                Abstract = null
            }
        };

        StagingDebugDumper.TryDump(ws, staged);

        var debugDir = Path.Combine(temp.Path, "_debug", "staging");
        Assert.True(Directory.Exists(debugDir));

        var jsonPath = Directory.EnumerateFiles(debugDir, "*.json").Single();
        var text = File.ReadAllText(jsonPath);

        Assert.Contains("\"TagsCsv\":\"one, two\"", text);
        Assert.Contains("hooks/article.json", text);
        Assert.Contains("\"Title\":\"Alpha\"", text);

        // Optional sanity: ensure it's valid JSON
        using var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement.TryGetProperty("Staging", out _));
    }

    private sealed class TempDir : System.IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lm_wpf_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
