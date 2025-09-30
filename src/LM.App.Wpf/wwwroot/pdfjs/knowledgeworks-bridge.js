const HOST_OBJECT_KEY = "knowledgeworksBridge";
const MESSAGE_READY = "ready";
const MESSAGE_SELECTION_CHANGED = "selection-changed";
const MESSAGE_NAV_CHANGED = "nav-changed";

let currentSelectionSnapshot = null;
let overlaySyncHandle = null;
const processedHighlights = new Set();
const RETRY_DELAY_MS = 50;
let pdfLoadCompleted = false;
let bridgeInitializationCompleted = false;

function getChromeWebView() {
  return window.chrome?.webview ?? null;
}

function getHostObject() {
  const chromeWebView = getChromeWebView();
  return chromeWebView?.hostObjects?.[HOST_OBJECT_KEY] ?? null;
}

function postMessage(message) {
  const chromeWebView = getChromeWebView();
  if (!chromeWebView) {
    return;
  }

  try {
    chromeWebView.postMessage(message);
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to post message", error);
  }
}

function getPageElementFromNode(node) {
  if (!node) {
    return null;
  }

  if (node.nodeType === Node.ELEMENT_NODE) {
    const element = /** @type {Element} */ (node);
    return element.closest?.(".page") ?? null;
  }

  if (node.parentElement) {
    return node.parentElement.closest?.(".page") ?? null;
  }

  return null;
}

function captureSelection() {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0 || selection.isCollapsed) {
    currentSelectionSnapshot = null;
    postMessage({ type: MESSAGE_SELECTION_CHANGED, selection: null });
    return;
  }

  const range = selection.getRangeAt(0).cloneRange();
  const text = selection.toString().trim();
  if (!text) {
    currentSelectionSnapshot = null;
    postMessage({ type: MESSAGE_SELECTION_CHANGED, selection: null });
    return;
  }

  const pageElement = getPageElementFromNode(selection.anchorNode) ?? getPageElementFromNode(selection.focusNode);
  const pageNumber = pageElement ? Number.parseInt(pageElement.dataset?.pageNumber ?? "", 10) : Number.NaN;
  const bounds = range.getBoundingClientRect();
  const pageBounds = pageElement?.getBoundingClientRect() ?? null;

  let relativeRect = null;
  if (bounds && pageBounds && bounds.width > 0 && bounds.height > 0) {
    relativeRect = {
      x: bounds.left - pageBounds.left,
      y: bounds.top - pageBounds.top,
      width: bounds.width,
      height: bounds.height,
    };
  }

  const snapshot = {
    text,
    pageNumber: Number.isNaN(pageNumber) ? null : pageNumber,
    rect: relativeRect,
  };

  currentSelectionSnapshot = snapshot;
  postMessage({ type: MESSAGE_SELECTION_CHANGED, selection: snapshot });
}

function escapeSelector(value) {
  if (typeof value !== "string") {
    return value;
  }

  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
    return CSS.escape(value);
  }

  return value.replace(/([\0-\x1F\x7F"'\\])/g, "\\$1");
}

function scrollToAnnotationDom(annotationId) {
  if (!annotationId) {
    return false;
  }

  const key = String(annotationId);
  const selector = escapeSelector(key);
  const target = document.getElementById(key) ?? document.querySelector(`[data-annotation-id="${selector}"]`);
  if (!target) {
    return false;
  }

  const pageElement = target.closest?.(".page");
  const pageNumber = pageElement ? Number.parseInt(pageElement.dataset?.pageNumber ?? "", 10) : Number.NaN;
  const app = window.PDFViewerApplication;

  if (!Number.isNaN(pageNumber) && app?.pdfViewer) {
    app.pdfViewer.currentPageNumber = pageNumber;
  }

  target.scrollIntoView({ behavior: "smooth", block: "center" });
  target.classList.add("kw-annotation-focus");
  window.setTimeout(() => target.classList.remove("kw-annotation-focus"), 800);
  return true;
}

function scheduleOverlaySync(storage) {
  if (overlaySyncHandle) {
    window.clearTimeout(overlaySyncHandle);
  }

  overlaySyncHandle = window.setTimeout(() => {
    overlaySyncHandle = null;
    void pushOverlaySnapshot(storage);
  }, 150);
}

async function pushOverlaySnapshot(storage) {
  const host = getHostObject();
  if (!host) {
    return;
  }

  try {
    const serializable = storage?.serializable ?? {};
    const map = serializable.map instanceof Map ? Object.fromEntries(serializable.map.entries()) : serializable.map ?? {};
    const payload = {
      overlay: map,
      hash: serializable.hash ?? "",
    };

    await host.SetOverlayAsync(JSON.stringify(payload));
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to persist overlay", error);
  }
}

async function handleHighlightEditorAdded(editor) {
  const host = getHostObject();
  if (!host || !editor) {
    return;
  }

  const annotationId = editor.annotationElementId ?? editor.id;
  if (!annotationId) {
    return;
  }

  const key = String(annotationId);
  if (processedHighlights.has(key)) {
    return;
  }

  const payload = {
    annotationId: key,
    pageNumber: (editor.pageIndex ?? 0) + 1,
    color: editor.color ?? null,
    textSnippet: currentSelectionSnapshot?.text ?? null,
  };

  try {
    await host.CreateHighlightAsync(JSON.stringify(payload));
    processedHighlights.add(key);
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to notify highlight creation", error);
  }
}

function patchAnnotationLayerForHighlights() {
  const viewerNamespace = window.pdfjsViewer;
  const layerPrototype = viewerNamespace?.AnnotationEditorLayer?.prototype;
  if (!layerPrototype || layerPrototype.__kwBridgePatched) {
    return;
  }

  const originalAdd = layerPrototype.add;
  layerPrototype.add = function add(editor) {
    originalAdd.call(this, editor);
    if (editor?.name === "highlightEditor") {
      queueMicrotask(() => handleHighlightEditorAdded(editor));
    }
  };

  Object.defineProperty(layerPrototype, "__kwBridgePatched", {
    value: true,
    configurable: false,
    enumerable: false,
  });
}

async function loadPdfFromHost() {
  if (pdfLoadCompleted) {
    return;
  }

  const host = getHostObject();
  if (!host) {
    window.setTimeout(() => void loadPdfFromHost(), RETRY_DELAY_MS);
    return;
  }

  const app = window.PDFViewerApplication;
  if (!app) {
    window.setTimeout(() => void loadPdfFromHost(), RETRY_DELAY_MS);
    return;
  }

  try {
    const initializedPromise = app.initializedPromise;
    if (initializedPromise?.then) {
      await initializedPromise;
    }

    const target = await host.LoadPdfAsync();
    const normalizedTarget = typeof target === "string" ? target.trim() : "";
    if (!normalizedTarget) {
      window.setTimeout(() => void loadPdfFromHost(), RETRY_DELAY_MS);
      return;
    }

    if (typeof app.open === "function") {
      if (app.url !== normalizedTarget) {
        await app.open({ url: normalizedTarget, originalUrl: normalizedTarget });
      }

      pdfLoadCompleted = true;
      return;
    }

    if (app.url === normalizedTarget) {
      pdfLoadCompleted = true;
      return;
    }

    window.setTimeout(() => void loadPdfFromHost(), RETRY_DELAY_MS);
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to load PDF from host", error);
    window.setTimeout(() => void loadPdfFromHost(), RETRY_DELAY_MS);
  }
}

function watchAnnotationStorage(app) {
  const storage = app?.pdfDocument?.annotationStorage;
  if (!storage) {
    return;
  }

  storage.onSetModified = () => scheduleOverlaySync(storage);
  storage.onResetModified = () => scheduleOverlaySync(storage);
}

async function initializeBridge() {
  if (bridgeInitializationCompleted) {
    return;
  }

  const app = window.PDFViewerApplication;
  if (!app) {
    window.setTimeout(() => void initializeBridge(), RETRY_DELAY_MS);
    return;
  }

  try {
    const initializedPromise = app.initializedPromise;
    if (initializedPromise?.then) {
      await initializedPromise;
    }

    if (bridgeInitializationCompleted) {
      return;
    }

    bridgeInitializationCompleted = true;

    postMessage({ type: MESSAGE_READY });

    document.addEventListener("selectionchange", () => captureSelection());
    app.eventBus?.on("pagechanging", evt => {
      postMessage({ type: MESSAGE_NAV_CHANGED, pageNumber: evt?.pageNumber ?? null });
    });

    patchAnnotationLayerForHighlights();
    watchAnnotationStorage(app);
    app.eventBus?.on("documentloaded", () => watchAnnotationStorage(app));
    void loadPdfFromHost();
  } catch (error) {
    console.error("knowledgeworks-bridge: initialization failed", error);
    bridgeInitializationCompleted = false;
    window.setTimeout(() => void initializeBridge(), RETRY_DELAY_MS);
  }
}

async function applyOverlay(snapshot) {
  const app = window.PDFViewerApplication;
  if (!app) {
    return false;
  }

  const storage = app.pdfDocument?.annotationStorage;
  if (!storage || typeof storage._setValues !== "function") {
    return false;
  }

  try {
    const payload = typeof snapshot === "string" ? JSON.parse(snapshot) : snapshot;
    if (!payload || typeof payload !== "object") {
      return false;
    }

    const map = payload.overlay ?? payload.annotationStorage ?? payload;
    if (!map || typeof map !== "object") {
      return false;
    }

    storage._setValues(map);
    storage.resetModified?.();
    return true;
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to apply overlay", error);
    return false;
  }
}

async function createHighlightFromHost(payload) {
  const host = getHostObject();
  if (!host) {
    return null;
  }

  try {
    const content = typeof payload === "string" ? payload : JSON.stringify(payload ?? {});
    return await host.CreateHighlightAsync(content);
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to create highlight via host", error);
    return null;
  }
}

async function getCurrentSelectionFromHost() {
  const host = getHostObject();
  if (!host) {
    return null;
  }

  try {
    const result = await host.GetCurrentSelectionAsync();
    return result;
  } catch (error) {
    console.error("knowledgeworks-bridge: failed to retrieve selection", error);
    return null;
  }
}

function requestOverlaySnapshot() {
  const app = window.PDFViewerApplication;
  const storage = app?.pdfDocument?.annotationStorage;
  if (!storage) {
    return;
  }

  void pushOverlaySnapshot(storage);
}

window.PdfBridge = {
  loadPdf: loadPdfFromHost,
  applyOverlay,
  scrollToAnnotation: scrollToAnnotationDom,
  createHighlight: createHighlightFromHost,
  getCurrentSelection: getCurrentSelectionFromHost,
  requestOverlaySnapshot,
};

if (document.readyState === "complete" || document.readyState === "interactive") {
  void initializeBridge();
} else {
  window.addEventListener("DOMContentLoaded", () => void initializeBridge(), { once: true });
}
