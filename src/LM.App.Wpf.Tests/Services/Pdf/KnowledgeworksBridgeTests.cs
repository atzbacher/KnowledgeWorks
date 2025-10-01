using System;
using System.IO;
using System.Text.Json;
using Jint;
using Jint.Native;
using Xunit;

namespace LM.App.Wpf.Tests.Services.Pdf
{
    public sealed class KnowledgeworksBridgeTests
    {
        [Fact]
        public void InitializeBridgeRetriesUntilHostObjectAvailable()
        {
            using var harness = KnowledgeworksBridgeHarness.Create();

            harness.InvokeInitializeBridge();

            Assert.True(harness.HasPendingTimers);

            harness.SetPdfViewerApplication();

            Assert.True(harness.RunNextTimer());

            harness.SetHostObject("app://entry.pdf");

            harness.DrainTimers();

            Assert.Equal(1, harness.LoadPdfInvocationCount);
            Assert.Equal("app://entry.pdf", harness.OpenedUrl);
            Assert.False(harness.RunNextTimer());
        }

        [Fact]
        public void LoadPdfFromHostRequestsHostWhenUrlChanges()
        {
            using var harness = KnowledgeworksBridgeHarness.Create();

            harness.SetPdfViewerApplication();
            harness.SetHostObject("app://first.pdf");

            harness.InvokePdfBridgeLoad();

            harness.DrainTimers();

            Assert.Equal(1, harness.LoadPdfInvocationCount);
            Assert.Equal("app://first.pdf", harness.OpenedUrl);

            harness.SetHostObject("app://second.pdf");

            harness.InvokePdfBridgeLoad();

            harness.DrainTimers();

            Assert.Equal(2, harness.LoadPdfInvocationCount);
            Assert.Equal("app://second.pdf", harness.OpenedUrl);
        }

        [Fact]
        public void LoadPdfFromHostRequestsHostWhenSameUrlReloaded()
        {
            using var harness = KnowledgeworksBridgeHarness.Create();

            harness.SetPdfViewerApplication();
            harness.SetHostObject("app://shared.pdf");

            harness.InvokePdfBridgeLoad();
            harness.DrainTimers();

            Assert.Equal(1, harness.LoadPdfInvocationCount);
            Assert.Equal("app://shared.pdf", harness.OpenedUrl);

            harness.InvokePdfBridgeLoad();
            harness.DrainTimers();

            Assert.Equal(2, harness.LoadPdfInvocationCount);
            Assert.Equal("app://shared.pdf", harness.OpenedUrl);
        }

        [Fact]
        public void LoadPdfFromHostWhenTargetUrlMissing()
        {
            using var harness = KnowledgeworksBridgeHarness.Create();

            harness.SetPdfViewerApplication();
            harness.SetHostObject("app://virtual.pdf");

            harness.InvokePdfBridgeLoad(string.Empty);
            harness.DrainTimers();

            Assert.Equal(1, harness.LoadPdfInvocationCount);
            Assert.Equal("app://virtual.pdf", harness.OpenedUrl);
        }

        private sealed class KnowledgeworksBridgeHarness : IDisposable
        {
            private readonly Engine _engine;
            private readonly JsValue _window;
            private bool _disposed;

            private int _loadPdfInvocationCount;
            private string? _openedUrl;

            private KnowledgeworksBridgeHarness(string scriptPath)
            {
                _engine = new Engine(options => options.CatchClrExceptions());

                _engine.Execute("var window = globalThis;");
                _window = _engine.GetValue("window");

                _engine.SetValue("__kwRecordLoad", new Action(() => _loadPdfInvocationCount++));
                _engine.SetValue("__kwRecordOpen", new Action<string>(url => _openedUrl = url ?? string.Empty));

                _engine.Execute(@"
                    window.document = { readyState: 'loading', addEventListener: function() {} };
                    window.addEventListener = function() {};
                    window.__kwTimers = { nextId: 1, queue: [] };
                    window.setTimeout = function(callback, delay) {
                        var id = window.__kwTimers.nextId++;
                        window.__kwTimers.queue.push({ id: id, callback: callback });
                        return id;
                    };
                    window.clearTimeout = function(handle) {
                        var id = Number(handle);
                        if (!isFinite(id)) {
                            return;
                        }
                        window.__kwTimers.queue = window.__kwTimers.queue.filter(function(entry) { return entry && entry.id !== id; });
                    };
                    window.__kwRunNextTimer = function() {
                        while (window.__kwTimers.queue.length > 0) {
                            var entry = window.__kwTimers.queue.shift();
                            if (entry && typeof entry.callback === 'function') {
                                entry.callback();
                                return entry.id;
                            }
                        }
                        return 0;
                    };
                    window.queueMicrotask = function(callback) {
                        if (typeof callback === 'function') {
                            callback();
                        }
                    };
                    window.chrome = { webview: { hostObjects: {}, postMessage: function() {} } };
                    window.console = { error: function() {}, log: function() {} };
                    globalThis.document = window.document;
                    globalThis.console = window.console;
                    globalThis.Node = { ELEMENT_NODE: 1 };
                    globalThis.CSS = undefined;
                ");

                var scriptContent = File.ReadAllText(scriptPath);
                _engine.Execute(scriptContent);
            }

            public static KnowledgeworksBridgeHarness Create()
            {
                var baseDir = AppContext.BaseDirectory;
                var scriptPath = Path.GetFullPath(Path.Combine(
                    baseDir,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "LM.App.Wpf",
                    "wwwroot",
                    "pdfjs",
                    "knowledgeworks-bridge.js"));

                return new KnowledgeworksBridgeHarness(scriptPath);
            }

            public void InvokeInitializeBridge()
            {
                var initialize = _engine.GetValue("initializeBridge");
                _engine.Invoke(initialize);
            }

            public void InvokePdfBridgeLoad(string? target = null)
            {
                var loadPdf = _engine.GetValue("window.PdfBridge.loadPdf");
                if (target is null)
                {
                    _engine.Invoke(loadPdf);
                }
                else
                {
                    _engine.Invoke(loadPdf, target);
                }

            }

            public bool HasPendingTimers => GetQueueLength() > 0;

            public bool RunNextTimer()
            {
                var result = _engine.GetValue("window.__kwRunNextTimer()");
                return ConvertToNumber(result) > 0;
            }

            public void DrainTimers()
            {
                while (RunNextTimer())
                {
                }
            }

            public int LoadPdfInvocationCount => _loadPdfInvocationCount;

            public string? OpenedUrl => _openedUrl;

            public void SetPdfViewerApplication()
            {
                _engine.Execute(@"
                    window.PDFViewerApplication = {
                        url: '',
                        initializedPromise: { then: function(callback) { if (callback) { callback(); } } },
                        eventBus: { on: function() {} },
                        pdfViewer: {},
                        open: function(args) {
                            if (typeof args === 'string') {
                                throw new Error('string overload not supported');
                            }

                            var next = '';
                            if (args && typeof args.url === 'string') {
                                next = args.url;
                            }

                            this.url = next;
                            __kwRecordOpen(next);
                        }
                    };
                    globalThis.PDFViewerApplication = window.PDFViewerApplication;
                ");
            }

            public void SetHostObject(string targetUrl)
            {
                var literal = JsonSerializer.Serialize(targetUrl ?? string.Empty);
                _engine.Execute($@"
                    window.chrome.webview.hostObjects.knowledgeworksBridge = {{
                        LoadPdfAsync: function() {{
                            __kwRecordLoad();
                            return {literal};
                        }},
                        CreateHighlightAsync: function() {{ return null; }},
                        GetCurrentSelectionAsync: function() {{ return null; }},
                        SetOverlayAsync: function() {{ }}
                    }};
                ");
            }

            private double GetQueueLength()
            {
                var value = _engine.GetValue("window.__kwTimers.queue.length");
                return ConvertToNumber(value);
            }

            private static double ConvertToNumber(JsValue value)
            {
                var obj = value.ToObject();
                return obj switch
                {
                    double d => d,
                    int i => i,
                    long l => l,
                    float f => f,
                    decimal m => (double)m,
                    _ => 0d,
                };
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
        }
    }
}
