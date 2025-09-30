using System;
using System.Collections.Generic;
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
                new NoopPersistenceService(),
                new NullOverlayReader(),
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

        [Fact]
        public void QueueOverlayForViewer_AppliesOverlayAfterViewerReady()
        {
            using var workspace = new TemporaryWorkspace();
            var orchestrator = new HookOrchestrator(workspace);
            var viewModel = new PdfViewerViewModel(
                orchestrator,
                new FixedUserContext("test-user"),
                new NoopPreviewStorage(),
                new NoopPersistenceService(),
                new NullOverlayReader(),
                workspace,
                new NullClipboardService());

            var bridge = new RecordingBridge();
            viewModel.WebViewBridge = bridge;

            InvokeQueueOverlayForViewer(viewModel, "{\"alpha\":1}");

            Assert.Null(bridge.LastOverlay);

            viewModel.HandleViewerReady();

            Assert.Equal("{\"alpha\":1}", bridge.LastOverlay);
        }

        [Fact]
        public async Task SetOverlayAsync_PersistsSnapshot()
        {
            using var workspace = new TemporaryWorkspace();
            var orchestrator = new HookOrchestrator(workspace);
            var persistence = new RecordingPersistenceService();
            var viewModel = new PdfViewerViewModel(
                orchestrator,
                new FixedUserContext("qa-user"),
                new NoopPreviewStorage(),
                persistence,
                new NullOverlayReader(),
                workspace,
                new NullClipboardService());

            viewModel.InitializeContext("entry-1", Path.Combine(workspace.Root, "doc.pdf"), "abcd");

            await viewModel.SetOverlayAsync("{\"overlay\":{\"foo\":1},\"hash\":\"abcd\"}").ConfigureAwait(true);

            await persistence.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal("entry-1", persistence.EntryId);
            Assert.Equal("abcd", persistence.PdfHash);
            Assert.Equal("{\"foo\":1}", persistence.OverlayJson);
            Assert.NotNull(persistence.PreviewImages);
            Assert.Empty(persistence.PreviewImages!);
        }

        [Fact]
        public async Task HandleHighlightPreviewAsync_PassesPreviewBytesToPersistence()
        {
            using var workspace = new TemporaryWorkspace();
            var orchestrator = new HookOrchestrator(workspace);
            var persistence = new RecordingPersistenceService();
            var viewModel = new PdfViewerViewModel(
                orchestrator,
                new FixedUserContext("qa-user"),
                new NoopPreviewStorage(),
                persistence,
                new NullOverlayReader(),
                workspace,
                new NullClipboardService());

            viewModel.InitializeContext("entry-7", Path.Combine(workspace.Root, "doc.pdf"), "ef01");

            await viewModel.HandleHighlightPreviewAsync("ann-1", new byte[] { 9, 9, 9 }, 10, 10).ConfigureAwait(true);
            await viewModel.SetOverlayAsync("{\"overlay\":{\"foo\":1},\"hash\":\"ef01\"}").ConfigureAwait(true);

            await persistence.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(persistence.PreviewImages!.ContainsKey("ann-1"));
            Assert.Equal(new byte[] { 9, 9, 9 }, persistence.PreviewImages["ann-1"]);
        }

        private static void InvokeQueueOverlayForViewer(PdfViewerViewModel viewModel, string overlayJson)
        {
            var method = typeof(PdfViewerViewModel).GetMethod("QueueOverlayForViewer", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(viewModel, new object[] { overlayJson });
        }

        private sealed class RecordingBridge : IPdfWebViewBridge
        {
            public int RequestCount { get; private set; }
            public string? LastOverlay { get; private set; }

            public Task ScrollToAnnotationAsync(string annotationId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task RequestDocumentLoadAsync(CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.CompletedTask;
            }

            public Task ApplyOverlayAsync(string overlayJson, CancellationToken cancellationToken)
            {
                LastOverlay = overlayJson;
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

        private sealed class NoopPersistenceService : IPdfAnnotationPersistenceService
        {
            public Task PersistAsync(string entryId, string pdfHash, string overlayJson, IReadOnlyDictionary<string, byte[]> previewImages, string? overlaySidecarRelativePath, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class RecordingPersistenceService : IPdfAnnotationPersistenceService
        {
            private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public string? EntryId { get; private set; }
            public string? PdfHash { get; private set; }
            public string? OverlayJson { get; private set; }
            public Dictionary<string, byte[]>? PreviewImages { get; private set; }

            public Task PersistAsync(string entryId, string pdfHash, string overlayJson, IReadOnlyDictionary<string, byte[]> previewImages, string? overlaySidecarRelativePath, CancellationToken cancellationToken)
            {
                EntryId = entryId;
                PdfHash = pdfHash;
                OverlayJson = overlayJson;
                PreviewImages = new Dictionary<string, byte[]>(previewImages, StringComparer.OrdinalIgnoreCase);
                _completion.TrySetResult(true);
                return Task.CompletedTask;
            }

            public async Task WaitAsync(TimeSpan timeout)
            {
                var completed = await Task.WhenAny(_completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
                if (!ReferenceEquals(completed, _completion.Task))
                {
                    throw new TimeoutException("Persistence was not invoked within the expected timeframe.");
                }
            }
        }

        private sealed class NullOverlayReader : IPdfAnnotationOverlayReader
        {
            public Task<string?> GetOverlayJsonAsync(string pdfHash, CancellationToken cancellationToken = default)
                => Task.FromResult<string?>(null);
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
