using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Library;
using LM.Core.Abstractions;
using LM.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryDocumentServiceTests
    {
        [Fact]
        public async Task OpenEntryAsync_UsesPdfViewerLauncherWhenAvailable()
        {
            var entry = new Entry
            {
                MainFilePath = "library/foo.pdf"
            };

            var workspace = new StubWorkspaceService();
            var launcher = new RecordingLauncher
            {
                Result = true
            };

            var service = new LibraryDocumentService(workspace, launcher);

            await service.OpenEntryAsync(entry);

            Assert.Equal(1, launcher.CallCount);
            Assert.Same(entry, launcher.Entry);
            Assert.Null(launcher.AttachmentId);
            Assert.Equal("library/foo.pdf", workspace.LastRelativePath);
        }

        [Fact]
        public async Task OpenEntryAsync_ThrowsWhenPathMissing()
        {
            var entry = new Entry
            {
                MainFilePath = string.Empty
            };

            var workspace = new StubWorkspaceService();
            var launcher = new RecordingLauncher();
            var service = new LibraryDocumentService(workspace, launcher);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenEntryAsync(entry));
            Assert.Equal(0, launcher.CallCount);
        }

        private sealed class StubWorkspaceService : IWorkSpaceService
        {
            public string? WorkspacePath => "/workspace";

            public string? LastRelativePath { get; private set; }

            public string GetWorkspaceRoot() => WorkspacePath ?? throw new InvalidOperationException("Workspace not set.");

            public string GetLocalDbPath() => Path.Combine(WorkspacePath ?? string.Empty, "library.db");

            public string GetAbsolutePath(string relativePath)
            {
                LastRelativePath = relativePath;
                return Path.Combine(WorkspacePath ?? string.Empty, relativePath);
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
                => Task.CompletedTask;
        }

        private sealed class RecordingLauncher : IPdfViewerLauncher
        {
            public int CallCount { get; private set; }

            public Entry? Entry { get; private set; }

            public string? AttachmentId { get; private set; }

            public bool Result { get; set; }

            public Task<bool> LaunchAsync(Entry entry, string? attachmentId = null)
            {
                CallCount++;
                Entry = entry;
                AttachmentId = attachmentId;
                return Task.FromResult(Result);
            }
        }
    }
}
