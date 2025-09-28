using System;
using System.IO;
using Xunit;

namespace LM.App.Wpf.Tests.ViewModels.Library
{
    public sealed class TessDataLocatorTests : IDisposable
    {
        private readonly string _root;
        private readonly string? _previousEnvironmentPrefix;
        private readonly string? _previousBootstrapSource;
        private readonly string? _previousBootstrapDirectory;
        private readonly string? _previousBootstrapDisabled;

        public TessDataLocatorTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "kw-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _previousEnvironmentPrefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
            _previousBootstrapSource = Environment.GetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_URL");
            _previousBootstrapDirectory = Environment.GetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DIR");
            _previousBootstrapDisabled = Environment.GetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DISABLED");
        }

        [Fact]
        public void Resolve_ReturnsWorkspaceTessdataDirectory_WhenTrainingDataIsImported()
        {
            var tessdata = Path.Combine(_root, ".knowledgeworks", "tessdata");
            Directory.CreateDirectory(tessdata);
            File.WriteAllText(Path.Combine(tessdata, "eng.traineddata"), "dummy");

            var resolved = LM.App.Wpf.ViewModels.Library.TessDataLocator.Resolve(_root);

            Assert.Equal(tessdata, resolved);
        }

        [Fact]
        public void Resolve_RespectsEnvironmentPrefix_WithNestedTessdata()
        {
            var envRoot = Path.Combine(_root, "env");
            var nested = Path.Combine(envRoot, "tessdata");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "eng.traineddata"), "dummy");

            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", envRoot);

            var resolved = LM.App.Wpf.ViewModels.Library.TessDataLocator.Resolve(null);

            Assert.Equal(nested, resolved);
        }

        [Fact]
        public void Resolve_FallsBackToDirectoryContainingTrainingFile()
        {
            var custom = Path.Combine(_root, ".knowledgeworks");
            Directory.CreateDirectory(custom);
            var file = Path.Combine(custom, "eng.traineddata");
            File.WriteAllText(file, "dummy");

            var resolved = LM.App.Wpf.ViewModels.Library.TessDataLocator.Resolve(_root);

            Assert.Equal(custom, resolved);
        }

        [Fact]
        public void Resolve_BootstrapsTrainingData_FromConfiguredSource()
        {
            var workspace = Path.Combine(_root, "workspace");
            Directory.CreateDirectory(workspace);

            var seedDirectory = Path.Combine(_root, "seed");
            Directory.CreateDirectory(seedDirectory);
            var seedFile = Path.Combine(seedDirectory, "eng.traineddata");
            File.WriteAllBytes(seedFile, new byte[2048]);

            Environment.SetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_URL", seedFile);
            Environment.SetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DISABLED", null);

            var resolved = LM.App.Wpf.ViewModels.Library.TessDataLocator.Resolve(workspace);

            Assert.NotNull(resolved);
            var targetFile = Path.Combine(resolved!, "eng.traineddata");
            Assert.True(File.Exists(targetFile));
            Assert.True(new FileInfo(targetFile).Length >= 2048);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(Path.GetTempPath());
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures so subsequent tests can continue.
            }

            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", _previousEnvironmentPrefix);
            Environment.SetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_URL", _previousBootstrapSource);
            Environment.SetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DIR", _previousBootstrapDirectory);
            Environment.SetEnvironmentVariable("KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DISABLED", _previousBootstrapDisabled);
        }
    }
}
