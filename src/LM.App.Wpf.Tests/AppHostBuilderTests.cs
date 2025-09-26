using System;
using System.Threading.Tasks;
using LM.App.Wpf.Application;
using LM.App.Wpf.Composition.Modules;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Review;
using LM.Core.Abstractions;
using LM.Infrastructure.FileSystem;
using LM.Review.Core.Services;
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
            .AddModule(new ReviewModule())
            .AddModule(new SearchModule())
            .Build();

        var entryStore = host.GetRequiredService<IEntryStore>();
        var pipeline = host.GetRequiredService<IAddPipeline>();
        var workflowService = host.GetRequiredService<IReviewWorkflowService>();
        var analyticsService = host.GetRequiredService<IReviewAnalyticsService>();
        var dashboardFactory = host.GetRequiredService<Func<ReviewDashboardViewModel>>();
        var stageFactory = host.GetRequiredService<Func<ReviewStageViewModel>>();

        Assert.NotNull(entryStore);
        Assert.NotNull(pipeline);
        Assert.NotNull(workflowService);
        Assert.NotNull(analyticsService);
        Assert.NotNull(dashboardFactory());
        Assert.NotNull(stageFactory());

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
