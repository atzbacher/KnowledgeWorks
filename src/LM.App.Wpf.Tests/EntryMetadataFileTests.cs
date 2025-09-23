using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LM.App.Wpf.Library;
using LM.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests
{
    public sealed class EntryMetadataFileTests
    {
        [Fact]
        public void TryEnsureExists_ReturnsTrueWhenFileAlreadyExists()
        {
            using var temp = new TempDir();
            Directory.CreateDirectory(temp.Path);
            var metadataPath = Path.Combine(temp.Path, "entry.json");
            File.WriteAllText(metadataPath, "{}");

            var entry = new Entry { Id = "abc123", Title = "Preserve" };

            var result = EntryMetadataFile.TryEnsureExists(entry, metadataPath);

            Assert.True(result);
            Assert.Equal("{}", File.ReadAllText(metadataPath));
        }

        [Fact]
        public void TryEnsureExists_CreatesMetadataWhenMissing()
        {
            using var temp = new TempDir();
            var metadataPath = Path.Combine(temp.Path, "entries", "abc123", "entry.json");
            var entry = new Entry
            {
                Id = "abc123",
                Title = "Created",
                Authors = new List<string> { "Someone" },
                AddedBy = "tester"
            };

            var result = EntryMetadataFile.TryEnsureExists(entry, metadataPath);

            Assert.True(result);
            Assert.True(File.Exists(metadataPath));

            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            Assert.Equal("Created", root.GetProperty("title").GetString());
            Assert.Equal("Someone", root.GetProperty("authors")[0].GetString());
            Assert.Equal("tester", root.GetProperty("addedBy").GetString());
        }

        [Fact]
        public void TryEnsureExists_CreatesParentDirectories()
        {
            using var temp = new TempDir();
            var metadataPath = Path.Combine(temp.Path, "nested", "deeper", "entry.json");
            var entry = new Entry { Id = "id", Title = "Anything" };

            var result = EntryMetadataFile.TryEnsureExists(entry, metadataPath);

            Assert.True(result);
            Assert.True(Directory.Exists(Path.GetDirectoryName(metadataPath)));
            Assert.True(File.Exists(metadataPath));
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kw_entrymeta_" + Guid.NewGuid().ToString("N"));
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }
}
