using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.Common;
using LM.App.Wpf.Services;
using LM.App.Wpf.ViewModels.Pdf;
using LM.Core.Abstractions;
using LM.Infrastructure.Hooks;
using Xunit;

namespace LM.App.Wpf.Tests.ViewModels.Pdf
{
    public sealed class PdfViewerViewModelTests
    {
        [Fact]
        public async Task LoadPdfCoreAsync_ReassigningSameSource_RequestsDocumentReload()
        {
            using var workspace = new TemporaryWorkspace();
            var orchestrator = new HookOrchestrator(workspace);
            var viewModel = new PdfViewerViewModel(
                orchestrator,
                new FixedUserContext("test-user"),
                new NoopPreviewStorage(),
                workspace,
                new NullClipboardService());

            var bridge = new RecordingBridge();
            viewModel.WebViewBridge = bridge;

            var pdfPath = Path.Combine(workspace.Root, "sample.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
            await File.WriteAllTextAsync(pdfPath, "pdf").ConfigureAwait(true);

            viewModel.PdfPath = pdfPath;
            await InvokeLoadPdfAsync(viewModel).ConfigureAwait(true);
            viewModel.HandleViewerReady();
            Assert.Equal(1, bridge.RequestCount);

            await InvokeLoadPdfAsync(viewModel).ConfigureAwait(true);
            viewModel.HandleViewerReady();

            Assert.Equal(2, bridge.RequestCount);
        }

        private static Task InvokeLoadPdfAsync(PdfViewerViewModel viewModel)
        {
            var method = typeof(PdfViewerViewModel).GetMethod("LoadPdfCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = method!.Invoke(viewModel, Array.Empty<object>()) as Task;
            return task ?? Task.CompletedTask;
        }

        private sealed class RecordingBridge : IPdfWebViewBridge
        {
            public int RequestCount { get; private set; }

            public Task ScrollToAnnotationAsync(string annotationId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task RequestDocumentLoadAsync(CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class TemporaryWorkspace : IWorkSpaceService, IDisposable
        {
            public string Root { get; } = Path.Combine(Path.GetTempPath(), "kw-pdf-viewer-tests-" + Guid.NewGuid().ToString("N"));

            public TemporaryWorkspace()
            {
                Directory.CreateDirectory(Root);
            }

            public string? WorkspacePath => Root;

            public string GetWorkspaceRoot() => Root;

            public string GetLocalDbPath() => Path.Combine(Root, "local.db");

            public string GetAbsolutePath(string relativePath)
            {
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(Root, normalized);
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                Directory.CreateDirectory(absoluteWorkspacePath);
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }

        private sealed class NoopPreviewStorage : IPdfAnnotationPreviewStorage
        {
            public Task<string> SaveAsync(string pdfHash, string annotationId, byte[] pngBytes, CancellationToken cancellationToken)
                => Task.FromResult(string.Empty);
        }

        private sealed class NullClipboardService : IClipboardService
        {
            public void SetText(string text)
            {
            }
        }

        private sealed class FixedUserContext : IUserContext
        {
            public FixedUserContext(string userName)
            {
                UserName = userName;
            }

            public string UserName { get; }
        }
    }
}
