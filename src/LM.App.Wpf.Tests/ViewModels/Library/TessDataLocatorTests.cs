using System;
using System.IO;
using Xunit;

namespace LM.App.Wpf.Tests.ViewModels.Library
{
    public sealed class TessDataLocatorTests : IDisposable
    {
        private readonly string _root;
        private readonly string? _previousEnvironmentPrefix;

        public TessDataLocatorTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "kw-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _previousEnvironmentPrefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
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
        }
    }
}
