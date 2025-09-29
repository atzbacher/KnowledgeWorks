using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LM.App.Wpf.Services.Pdf;
using Xunit;

public sealed class WebView2RuntimeBootstrapperTests
{
    [Fact]
    public void EnumerateLoaderProbePaths_ReturnsRuntimeAndRoot_ForX64()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "kw", "viewer");
        var paths = WebView2RuntimeBootstrapper
            .EnumerateLoaderProbePaths(baseDir, Architecture.X64)
            .ToArray();

        Assert.Equal(2, paths.Length);
        Assert.Equal(Path.Combine(baseDir, "runtimes", "win-x64", "native", "WebView2Loader.dll"), paths[0]);
        Assert.Equal(Path.Combine(baseDir, "WebView2Loader.dll"), paths[1]);
    }

    [Fact]
    public void EnumerateLoaderProbePaths_FallsBackToRoot_WhenArchitectureUnknown()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "kw", "viewer");
        var paths = WebView2RuntimeBootstrapper
            .EnumerateLoaderProbePaths(baseDir, Architecture.Arm)
            .ToArray();

        Assert.Single(paths);
        Assert.Equal(Path.Combine(baseDir, "WebView2Loader.dll"), paths[0]);
    }
}
