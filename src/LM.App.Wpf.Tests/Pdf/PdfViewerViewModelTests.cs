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

namespace LM.App.Wpf.Tests.Pdf
{
    public sealed class PdfViewerViewModelTests
    {
        [Fact]
        public void HandleViewerReady_ReloadsDocumentWhenViewerReinitializes()
        {
            using var workspace = new TestWorkspaceService();
            var orchestrator = new HookOrchestrator(workspace);
            var viewModel = new PdfViewerViewModel(
                orchestrator,
                new TestUserContext(),
                new TestPreviewStorage(),
                workspace,
                new TestClipboardService());

            var bridge = new StubWebViewBridge();
            viewModel.WebViewBridge = bridge;

            var documentSourceProperty = typeof(PdfViewerViewModel).GetProperty(
                nameof(PdfViewerViewModel.DocumentSource),
                BindingFlags.Instance | BindingFlags.Public);
            var setter = documentSourceProperty?.GetSetMethod(nonPublic: true);
            Assert.NotNull(setter);

            setter!.Invoke(viewModel, new object?[] { new Uri("file:///tmp/sample.pdf", UriKind.Absolute) });
            viewModel.UpdateVirtualDocumentSource(new Uri("https://viewer-documents.knowledgeworks/token/sample.pdf", UriKind.Absolute));

            viewModel.HandleViewerReady();
            viewModel.HandleViewerReady();

            Assert.Equal(2, bridge.RequestDocumentLoadAsyncCount);
        }

        private sealed class StubWebViewBridge : IPdfWebViewBridge
        {
            public int RequestDocumentLoadAsyncCount { get; private set; }

            public Task RequestDocumentLoadAsync(CancellationToken cancellationToken)
            {
                RequestDocumentLoadAsyncCount++;
                return Task.CompletedTask;
            }

            public Task ScrollToAnnotationAsync(string annotationId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class TestUserContext : IUserContext
        {
            public string UserName => "TestUser";
        }

        private sealed class TestPreviewStorage : IPdfAnnotationPreviewStorage
        {
            public Task<string> SaveAsync(string pdfHash, string annotationId, byte[] pngBytes, CancellationToken cancellationToken)
            {
                return Task.FromResult("preview.png");
            }
        }

        private sealed class TestClipboardService : IClipboardService
        {
            public void SetText(string text)
            {
            }
        }

        private sealed class TestWorkspaceService : IWorkSpaceService, IDisposable
        {
            private readonly string _root;

            public TestWorkspaceService()
            {
                _root = Path.Combine(Path.GetTempPath(), "kw-pdf-vm-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);
            }

            public string? WorkspacePath => _root;

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_root))
                    {
                        Directory.Delete(_root, recursive: true);
                    }
                }
                catch
                {
                }
            }

            public Task EnsureWorkspaceAsync(string absoluteWorkspacePath, CancellationToken ct = default)
            {
                Directory.CreateDirectory(absoluteWorkspacePath);
                return Task.CompletedTask;
            }

            public string GetAbsolutePath(string relativePath)
            {
                return Path.Combine(_root, relativePath);
            }

            public string GetLocalDbPath()
            {
                return Path.Combine(_root, "local.db");
            }

            public string GetWorkspaceRoot()
            {
                return _root;
            }
        }
    }
}
