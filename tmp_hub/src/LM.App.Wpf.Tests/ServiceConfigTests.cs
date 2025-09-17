using System.Threading.Tasks;
using Xunit;
using LM.Infrastructure.FileSystem;
using LM.App.Wpf.Composition;

public class ServiceConfigTests
{
    [Fact]
    public async Task Build_ReturnsPipeline_And_Store_ThatInitialize()
    {
        using var temp = new TempDir();
        var ws = new WorkspaceService();
        await ws.EnsureWorkspaceAsync(temp.Path);

        var s = ServiceConfig.Build(ws);
        Assert.NotNull(s.Pipeline);
        Assert.NotNull(s.Store);

        await s.Store.InitializeAsync();
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
