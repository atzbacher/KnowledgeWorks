using System.Threading.Tasks;
using LM.App.Wpf.Application;
using LM.App.Wpf.Composition.Modules;
using LM.App.Wpf.ViewModels;
using LM.Core.Abstractions;
using LM.Infrastructure.FileSystem;
using Xunit;

public class AppHostBuilderTests
{
    [Fact]
    public async Task Build_ComposesCoreServices()
    {
        using var temp = new TempDir();
        var workspace = new WorkspaceService();
        await workspace.EnsureWorkspaceAsync(temp.Path);

        using var host = AppHostBuilder.Create()
            .AddModule(new CoreModule(workspace))
            .AddModule(new AddModule())
            .AddModule(new LibraryModule())
            .AddModule(new SearchModule())
            .Build();

        var entryStore = host.GetRequiredService<IEntryStore>();
        var pipeline = host.GetRequiredService<IAddPipeline>();

        Assert.NotNull(entryStore);
        Assert.NotNull(pipeline);

        await entryStore.InitializeAsync();
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
