const INITIALIZE_RETRY_DELAY_MS = 200;
const OVERLAY_FLUSH_DELAY_MS = 400;
const MAX_SELECTION_LENGTH = 4096;

var retryHandle = 0;
var overlayFlushHandle = 0;
var pendingOverlayPayload = null;
var lastOverlayHash = null;
var readyPosted = false;
var knownAnnotationIds = new Set();

function registerAllowedDocumentOrigins() {
  if (typeof window === "undefined") {
    return;
  }

  var allowed = window.KnowledgeWorksAllowedDocumentOrigins;
  var origins = Array.isArray(allowed) ? allowed.slice() : [];
  var documentOrigin = "https://viewer-documents.knowledgeworks";

  if (!origins.includes(documentOrigin)) {
    origins.push(documentOrigin);
  }

  window.KnowledgeWorksAllowedDocumentOrigins = origins;
}

registerAllowedDocumentOrigins();

function getChromeWebView() {
  if (typeof window === "undefined") {
    return null;
  }

  if (!window.chrome || !window.chrome.webview) {
    return null;
  }

  return window.chrome.webview;
}

function getHostObject() {
  var webview = getChromeWebView();
  if (!webview || !webview.hostObjects) {
    return null;
  }

  return webview.hostObjects.knowledgeworksBridge || null;
}

function getViewerApplication() {
  if (typeof window === "undefined") {
    return null;
  }

  return window.PDFViewerApplication || null;
}

function postMessage(message) {
  if (!message || typeof message !== "object") {
    return;
  }

  var webview = getChromeWebView();
  if (!webview) {
    return;
  }

  try {
    webview.postMessage(message);
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to post message", error);
  }
}

function scheduleRetry() {
  if (retryHandle) {
    return;
  }

  retryHandle = window.setTimeout(function () {
    retryHandle = 0;
    initializeBridge();
  }, INITIALIZE_RETRY_DELAY_MS);
}

function ensurePdfBridge() {
  if (typeof window === "undefined") {
    return null;
  }

  if (window.PdfBridge && window.PdfBridge.__kwInitialized) {
    return window.PdfBridge;
  }

  var bridge = {
    __kwInitialized: true,
    loadPdf: function (target) {
      void loadPdfInternal(target);
    },
    applyOverlay: function (payload) {
      pendingOverlayPayload = payload == null ? null : payload;
      void applyOverlayInternal();
    },
  };

  window.PdfBridge = bridge;
  return bridge;
}

async function loadPdfInternal(targetUrl) {
  try {
    var app = getViewerApplication();
    if (!app) {
      console.warn("knowledgeworks-bridge: viewer application not available");
      return;
    }

    await app.initializedPromise;

    var url = typeof targetUrl === "string" ? targetUrl.trim() : "";
    if (url) {
      console.log("knowledgeworks-bridge: loading PDF from provided URL");
    } else {
      var host = getHostObject();
      if (!host || typeof host.LoadPdfAsync !== "function") {
        console.warn("knowledgeworks-bridge: host not ready to supply PDF URL");
        return;
      }

      console.log("knowledgeworks-bridge: requesting PDF URL from host");
      var result = host.LoadPdfAsync();
      var resolved = typeof result === "string" ? result : await Promise.resolve(result);
      if (typeof resolved === "string") {
        url = resolved.trim();
      }

      if (!url) {
        console.warn("knowledgeworks-bridge: host returned an empty PDF URL");
        return;
      }

      console.log("knowledgeworks-bridge: opening host-provided PDF URL");
    }

    await app.open({
      url: url,
      originalUrl: url,
    });
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to load PDF", error);
  }
}

function registerSelectionHandlers(app) {
  function emitSelection() {
    try {
      var selection = window.getSelection ? window.getSelection() : null;
      if (!selection || selection.isCollapsed) {
        postMessage({ type: "selection-changed", selection: null });
        return;
      }

      var text = selection.toString();
      if (typeof text === "string") {
        text = text.replace(/\s+/g, " ").trim();
      }

      if (!text) {
        postMessage({ type: "selection-changed", selection: null });
        return;
      }

      if (text.length > MAX_SELECTION_LENGTH) {
        text = text.slice(0, MAX_SELECTION_LENGTH);
      }

      var pageNumber = null;
      if (app && app.pdfViewer && typeof app.pdfViewer.currentPageNumber === "number") {
        pageNumber = app.pdfViewer.currentPageNumber;
      }

      postMessage({
        type: "selection-changed",
        selection: {
          text: text,
          pageNumber: pageNumber,
        },
      });
    } catch (error) {
      console.error("knowledgeworks-bridge: failed to publish selection", error);
    }
  }

  document.addEventListener("selectionchange", emitSelection);

  if (app && app.eventBus && typeof app.eventBus.on === "function") {
    app.eventBus.on("textlayerrendered", emitSelection);
  }
}

function registerNavigationHandlers(app) {
  if (!app || !app.eventBus || typeof app.eventBus.on !== "function") {
    return;
  }

  app.eventBus.on("pagechanging", function (evt) {
    var pageNumber = null;
    if (evt && typeof evt.pageNumber === "number" && isFinite(evt.pageNumber)) {
      pageNumber = evt.pageNumber;
    }

    postMessage({ type: "nav-changed", pageNumber: pageNumber });
  });
}

function monitorAnnotationStorage(app) {
    console.log("🔍 monitorAnnotationStorage called");

    var storage = app && app.pdfDocument ? app.pdfDocument.annotationStorage : null;
    if (!storage) {
        console.error("   ❌ No annotation storage found!");
        console.error("   app:", app);
        console.error("   app.pdfDocument:", app ? app.pdfDocument : "no app");
        return false;
    }

    console.log("   ✓ Annotation storage exists");
    console.log("   Storage has onSetModified:", typeof storage.onSetModified);
    console.log("   Storage has onResetModified:", typeof storage.onResetModified);

    // Check if already hooked
    if (storage.__kwBridgeMonitored) {
        console.log("   ⚠️ Storage already being monitored");
        return true;
    }

    function scheduleFlush() {
        console.log("⏰ scheduleFlush called");

        if (overlayFlushHandle) {
            console.log("   Clearing existing timeout:", overlayFlushHandle);
            window.clearTimeout(overlayFlushHandle);
        }

        overlayFlushHandle = window.setTimeout(function () {
            console.log("⏱️ Flush timeout triggered! Calling flushOverlayAsync...");
            overlayFlushHandle = 0;
            void flushOverlayAsync(storage);
        }, OVERLAY_FLUSH_DELAY_MS);

        console.log("   ✓ Timeout scheduled with handle:", overlayFlushHandle, "delay:", OVERLAY_FLUSH_DELAY_MS, "ms");
    }

    var previousSet = storage.onSetModified;
    storage.onSetModified = function () {
        console.log("📝 storage.onSetModified triggered!");

        if (typeof previousSet === "function") {
            console.log("   Calling previous onSetModified...");
            previousSet();
        }

        scheduleFlush();
    };

    var previousReset = storage.onResetModified;
    storage.onResetModified = function () {
        console.log("🔄 storage.onResetModified triggered!");

        if (typeof previousReset === "function") {
            console.log("   Calling previous onResetModified...");
            previousReset();
        }

        scheduleFlush();
    };

    // Mark as monitored
    storage.__kwBridgeMonitored = true;

    console.log("✅ monitorAnnotationStorage setup complete");
    return true;
}

async function flushOverlayAsync(storage) {
    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    console.log("💾 flushOverlayAsync CALLED!");
    console.log("   Storage:", storage);

    var host = getHostObject();
    if (!host) {
        console.error("   ❌ NO HOST OBJECT! Cannot flush overlay.");
        console.error("   window.chrome:", window.chrome);
        console.error("   window.chrome?.webview:", window.chrome ? window.chrome.webview : "no chrome");
        console.error("   window.chrome?.webview?.hostObjects:", window.chrome && window.chrome.webview ? window.chrome.webview.hostObjects : "no webview");
        return;
    }

    console.log("   ✓ Host object exists:", host);
    console.log("   Host has SetOverlayAsync:", typeof host.SetOverlayAsync);

    try {
        var serializable = storage.serializable;
        console.log("   Serializable:", serializable);
        console.log("   Serializable.map:", serializable ? serializable.map : "no serializable");
        console.log("   Is Map:", serializable && serializable.map instanceof Map);

        var overlay = {};

        if (serializable && serializable.map instanceof Map) {
            console.log("   📊 Converting Map to object...");
            console.log("   Map size:", serializable.map.size);

            serializable.map.forEach(function (value, key) {
                console.log("      Entry:", key, "=", value);
                overlay[key] = value;
            });

            console.log("   ✓ Converted overlay object:", overlay);
            console.log("   Overlay keys count:", Object.keys(overlay).length);
        } else {
            console.log("   ⚠️ No serializable map - overlay will be empty");
        }

        var hash = serializable && typeof serializable.hash !== "undefined"
            ? serializable.hash
            : null;

        console.log("   Hash:", hash);

        var payload = {
            overlay: overlay,
            hash: hash,
        };

        console.log("   📦 Payload to send:", JSON.stringify(payload).substring(0, 200), "...");
        console.log("   Payload overlay keys:", Object.keys(payload.overlay).length);
        console.log("   Payload hash:", payload.hash);

        // Check for duplicate hash
        if (payload.hash && payload.hash === lastOverlayHash) {
            console.log("   ⏭️ SKIPPING - Same hash as last time:", lastOverlayHash);
            return;
        }

        lastOverlayHash = payload.hash ? payload.hash : null;
        console.log("   Updated lastOverlayHash to:", lastOverlayHash);

        console.log("   🚀 CALLING host.SetOverlayAsync NOW!");
        var payloadString = JSON.stringify(payload);
        console.log("   Payload string length:", payloadString.length);

        var result = host.SetOverlayAsync(payloadString);
        console.log("   SetOverlayAsync returned:", result);

        await Promise.resolve(result);
        console.log("   ✅ SetOverlayAsync completed successfully!");

    } catch (error) {
        console.error("   ❌ ERROR in flushOverlayAsync:", error);
        console.error("   Error stack:", error.stack);
    }

    console.log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
}

function patchAnnotationManager() {
    console.log("patchAnnotationManager called");

    if (typeof window === "undefined") {
        console.error("window is undefined!");
        return;
    }

    // CRITICAL CHECK: pdfjsViewer must exist
    if (!window.pdfjsViewer) {
        console.error("❌ window.pdfjsViewer is NOT available yet!");
        console.error("   This is why highlights don't work.");
        console.error("   Will retry in 200ms...");

        // Retry after a delay
        setTimeout(patchAnnotationManager, 200);
        return;
    }

    console.log("✓ window.pdfjsViewer exists");

    var viewerNs = window.pdfjsViewer;

    if (!viewerNs.AnnotationEditorUIManager) {
        console.error("❌ AnnotationEditorUIManager not available yet!");
        console.error("   Will retry in 200ms...");
        setTimeout(patchAnnotationManager, 200);
        return;
    }

    console.log("✓ AnnotationEditorUIManager exists");

    var proto = viewerNs.AnnotationEditorUIManager.prototype;

    if (proto.__kwBridgePatched) {
        console.log("✓ Already patched");
        return;
    }

    if (!proto.addToAnnotationStorage) {
        console.error("❌ addToAnnotationStorage method not found!");
        return;
    }

    console.log("✓ addToAnnotationStorage exists, applying patch...");

    var originalAdd = proto.addToAnnotationStorage;
    proto.addToAnnotationStorage = function (editor) {
        console.log("📝 addToAnnotationStorage called!", editor?.name);

        originalAdd.call(this, editor);

        if (!editor || typeof editor !== "object" || editor.name !== "highlightEditor") {
            console.log("   Not a highlight, skipping");
            return;
        }

        console.log("   ✓ IS A HIGHLIGHT! Calling handleHighlightCreatedAsync...");

        queueMicrotask(function () {
            void handleHighlightCreatedAsync(editor);
        });
    };

    Object.defineProperty(proto, "__kwBridgePatched", {
        configurable: false,
        enumerable: false,
        writable: false,
        value: true,
    });

    console.log("✅ PATCH APPLIED SUCCESSFULLY!");
}

async function handleHighlightCreatedAsync(editor) {
  try {
    var host = getHostObject();
    if (!host) {
      return;
    }

    var annotationId = editor.annotationElementId || editor.id;
    if (!annotationId || knownAnnotationIds.has(annotationId)) {
      return;
    }

    knownAnnotationIds.add(annotationId);

    var selectionSnapshot = await getSelectionSnapshotAsync();
    var pageNumber = null;
    if (typeof editor.pageIndex === "number" && isFinite(editor.pageIndex)) {
      pageNumber = editor.pageIndex + 1;
    } else if (selectionSnapshot && typeof selectionSnapshot.pageNumber === "number") {
      pageNumber = selectionSnapshot.pageNumber;
    }

    var textSnippet = null;
    if (selectionSnapshot && typeof selectionSnapshot.text === "string") {
      textSnippet = selectionSnapshot.text;
    } else if (editor && typeof editor.text === "string") {
      textSnippet = editor.text;
    }

    var payload = {
      annotationId: annotationId,
      pageNumber: pageNumber,
      textSnippet: textSnippet,
      color: editor && editor.color ? editor.color : null,
    };

    await Promise.resolve(host.CreateHighlightAsync(JSON.stringify(payload)));
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to notify highlight creation", error);
  }
}

async function getSelectionSnapshotAsync() {
  try {
    var host = getHostObject();
    if (!host || typeof host.GetCurrentSelectionAsync !== "function") {
      return null;
    }

    var result = host.GetCurrentSelectionAsync();
    var json = typeof result === "string" ? result : await Promise.resolve(result);
    if (!json) {
      return null;
    }

    return JSON.parse(json);
  } catch (error) {
    return null;
  }
}

function parseOverlayPayload(payload) {
  if (!payload) {
    return null;
  }

  try {
    if (typeof payload === "string") {
      return JSON.parse(payload);
    }

    if (typeof payload === "object") {
      return payload;
    }
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to parse overlay payload", error);
  }

  return null;
}

async function restoreAnnotationStorage(app, overlayPayload) {
  try {
    var overlay = overlayPayload && typeof overlayPayload === "object" && overlayPayload.overlay ? overlayPayload.overlay : overlayPayload;
    if (!overlay || typeof overlay !== "object") {
      return;
    }

    var storage = app && app.pdfDocument ? app.pdfDocument.annotationStorage : null;
    if (!storage) {
      return;
    }

    for (var iterator = storage[Symbol.iterator]();;) {
      var next = iterator.next();
      if (next.done) {
        break;
      }

      var entry = next.value;
      if (entry && entry.length > 0) {
        storage.remove(entry[0]);
      }
    }

    if (overlay instanceof Map) {
      overlay.forEach(function (value, key) {
        storage.setValue(key, value);
      });
    } else {
      for (var key in overlay) {
        if (Object.prototype.hasOwnProperty.call(overlay, key)) {
          storage.setValue(key, overlay[key]);
        }
      }
    }

    storage.resetModified();
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to restore overlay", error);
  }
}

async function applyOverlayInternal() {
  if (!pendingOverlayPayload) {
    return;
  }

  var payload = pendingOverlayPayload;

  try {
    var app = getViewerApplication();
    if (!app) {
      return;
    }

    await app.initializedPromise;
    var parsed = parseOverlayPayload(payload);
    if (!parsed) {
      return;
    }

    await restoreAnnotationStorage(app, parsed);
    pendingOverlayPayload = null;
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to apply overlay", error);
  }
}

function notifyReadyOnce() {
  if (readyPosted) {
    return;
  }

  readyPosted = true;
  postMessage({ type: "ready" });
}

function initializeBridge() {
    console.log("knowledgeworks-bridge: initializeBridge called");
    ensurePdfBridge();

  var host = getHostObject();
    if (!host) {
    console.log("knowledgeworks-bridge: host not ready, retrying...");
    scheduleRetry();
    return;
  }

  console.log("knowledgeworks-bridge: host found");

  var app = getViewerApplication();
  if (!app) {
    console.log("knowledgeworks-bridge: PDFViewerApplication not ready, retrying...");
    scheduleRetry();
    return;
  }

  console.log("knowledgeworks-bridge: PDFViewerApplication found, waiting for initialization...");

    app.initializedPromise.then(function () {
        console.log("knowledgeworks-bridge: viewer fully initialized");
        notifyReadyOnce();

        var editorButtons = document.getElementById("editorModeButtons");
        if (editorButtons) {
            editorButtons.classList.remove("hidden");
            console.log("knowledgeworks-bridge: editor buttons unhidden");
        }

        setupAnnotationEditorListener();
        registerSelectionHandlers(app);
        registerNavigationHandlers(app);

        // CRITICAL: Setup storage monitoring with proper event handling
        console.log("🎯 Setting up storage monitoring...");

        // Try immediately first
        if (!setupStorageMonitoring()) {
            console.log("   Storage not ready yet, setting up event listeners...");

            if (app.eventBus && typeof app.eventBus.on === "function") {
                // Listen for document loaded event
                app.eventBus.on("documentloaded", function () {
                    console.log("📄 documentloaded event fired! Attempting storage setup...");
                    setTimeout(function () {
                        if (setupStorageMonitoring()) {
                            console.log("   ✅ Storage monitoring active after documentloaded");
                        }
                    }, 100);
                });

                // Also try after document init
                app.eventBus.on("documentinit", function () {
                    console.log("📄 documentinit event fired! Attempting storage setup...");
                    setTimeout(function () {
                        setupStorageMonitoring();
                    }, 100);
                });

                // And after pages init
                app.eventBus.on("pagesinit", function () {
                    console.log("📄 pagesinit event fired! Attempting storage setup...");
                    setTimeout(function () {
                        setupStorageMonitoring();
                    }, 100);
                });

                console.log("   ✓ Event listeners registered");
            }

            // Fallback: poll periodically
            console.log("   ⏰ Starting polling fallback...");
            var pollAttempts = 0;
            var maxPolls = 20;
            var pollInterval = setInterval(function () {
                pollAttempts++;
                console.log("   📡 Poll attempt", pollAttempts);

                if (setupStorageMonitoring()) {
                    console.log("   ✅ Storage found via polling! Stopping poll.");
                    clearInterval(pollInterval);
                } else if (pollAttempts >= maxPolls) {
                    console.error("   ❌ Gave up after", maxPolls, "poll attempts");
                    clearInterval(pollInterval);
                }
            }, 500);
        } else {
            console.log("   ✅ Storage monitoring active immediately");
        }

        // Patch annotation manager
        console.log("Waiting 500ms before patching annotation manager...");
        setTimeout(function () {
            console.log("Now attempting to patch annotation manager...");
            patchAnnotationManager();
        }, 500);

        void applyOverlayInternal();
        void loadPdfInternal();
    }).catch(function (error) {
        console.error("knowledgeworks-bridge: viewer initialization failed", error);
    });
}

if (document.readyState === "complete" || document.readyState === "interactive") {
  initializeBridge();
} else {
  window.addEventListener("DOMContentLoaded", function () { initializeBridge(); }, { once: true });
}

if (typeof window !== "undefined") {
  window.initializeBridge = initializeBridge;
}

function setupAnnotationEditorListener() {
    var app = getViewerApplication();
    if (!app || !app.eventBus) {
        return;
    }

    app.eventBus.on("annotationeditoruimanager", function (evt) {
        console.log("📢 annotationeditoruimanager event fired!");
        console.log("   UI Manager:", evt.uiManager);

        // Now we KNOW the manager exists, patch it immediately
        setTimeout(function () {
            patchAnnotationManager();
        }, 100);
    });

    console.log("✓ Listening for annotationeditoruimanager event");
}


function setupStorageMonitoring() {
    console.log("🎯 setupStorageMonitoring called");

    var app = getViewerApplication();
    if (!app) {
        console.log("   ⚠️ App not available");
        return false;
    }

    if (!app.pdfDocument) {
        console.log("   ⚠️ pdfDocument is null - PDF not loaded yet!");
        return false;
    }

    if (!app.pdfDocument.annotationStorage) {
        console.log("   ⚠️ annotationStorage is null!");
        return false;
    }

    console.log("   ✅ Storage exists! Setting up monitoring...");
    return monitorAnnotationStorage(app);
}

