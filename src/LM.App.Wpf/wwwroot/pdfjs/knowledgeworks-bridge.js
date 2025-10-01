const INITIALIZE_RETRY_DELAY_MS = 200;
const OVERLAY_FLUSH_DELAY_MS = 400;
const MAX_SELECTION_LENGTH = 4096;

var retryHandle = 0;
var overlayFlushHandle = 0;
var pendingOverlayPayload = null;
var lastOverlayHash = null;
var readyPosted = false;
var knownAnnotationIds = new Set();

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
      return;
    }

    await app.initializedPromise;

    var url = typeof targetUrl === "string" ? targetUrl.trim() : "";
    if (!url) {
      var host = getHostObject();
      if (!host) {
        return;
      }

      var result = host.LoadPdfAsync();
      url = typeof result === "string" ? result : await Promise.resolve(result);
      if (typeof url === "string") {
        url = url.trim();
      }
    }

    if (!url) {
      return;
    }

    try {
      await app.open({ url: url });
    } catch (error) {
      await app.open(url);
    }
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
  var storage = app && app.pdfDocument ? app.pdfDocument.annotationStorage : null;
  if (!storage) {
    return;
  }

  function scheduleFlush() {
    if (overlayFlushHandle) {
      window.clearTimeout(overlayFlushHandle);
    }

    overlayFlushHandle = window.setTimeout(function () {
      overlayFlushHandle = 0;
      void flushOverlayAsync(storage);
    }, OVERLAY_FLUSH_DELAY_MS);
  }

  var previousSet = storage.onSetModified;
  storage.onSetModified = function () {
    if (typeof previousSet === "function") {
      previousSet();
    }

    scheduleFlush();
  };

  var previousReset = storage.onResetModified;
  storage.onResetModified = function () {
    if (typeof previousReset === "function") {
      previousReset();
    }

    scheduleFlush();
  };
}

async function flushOverlayAsync(storage) {
  var host = getHostObject();
  if (!host) {
    return;
  }

  try {
    var serializable = storage.serializable;
    var overlay = {};

    if (serializable && serializable.map instanceof Map) {
      serializable.map.forEach(function (value, key) {
        overlay[key] = value;
      });
    }

    var payload = {
      overlay: overlay,
      hash: serializable && typeof serializable.hash !== "undefined" ? serializable.hash : null,
    };

    if (payload.hash && payload.hash === lastOverlayHash) {
      return;
    }

    lastOverlayHash = payload.hash ? payload.hash : null;
    await Promise.resolve(host.SetOverlayAsync(JSON.stringify(payload)));
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to persist overlay", error);
  }
}

function patchAnnotationManager() {
  if (typeof window === "undefined" || !window.pdfjsViewer) {
    return;
  }

  var viewerNs = window.pdfjsViewer;
  var proto = viewerNs.AnnotationEditorUIManager ? viewerNs.AnnotationEditorUIManager.prototype : null;
  if (!proto || proto.__kwBridgePatched) {
    return;
  }

  var originalAdd = proto.addToAnnotationStorage;
  proto.addToAnnotationStorage = function (editor) {
    originalAdd.call(this, editor);

    if (!editor || typeof editor !== "object" || editor.name !== "highlightEditor") {
      return;
    }

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
  ensurePdfBridge();

  var app = getViewerApplication();
  var host = getHostObject();

  if (!app || !host) {
    scheduleRetry();
    return;
  }

  app.initializedPromise.then(function () {
    notifyReadyOnce();
    registerSelectionHandlers(app);
    registerNavigationHandlers(app);
    monitorAnnotationStorage(app);
    patchAnnotationManager();
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
