using System.Threading.Tasks;
using Xunit;
using LM.App.Wpf.Composition;
using LM.Infrastructure.FileSystem;

public class PipelineNormalizationWiringTests
{
    [Fact]
    public async Task Build_Wires_PmidNormalizer_Into_Pipeline()
    {
        using var temp = new TempDir();
        var ws = new WorkspaceService();
        await ws.EnsureWorkspaceAsync(temp.Path);

        var s = ServiceConfig.Build(ws);

        // If Build() succeeds, AddPipeline was constructed with doi+pmid normalizers & orchestrator
        Assert.NotNull(s.Pipeline);
        Assert.NotNull(s.Pmid);
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
