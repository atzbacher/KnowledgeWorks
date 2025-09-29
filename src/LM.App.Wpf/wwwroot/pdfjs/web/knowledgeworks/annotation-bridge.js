const MESSAGE_TYPE_SCROLL = "scroll-to-annotation";

function getChromeWebView() {
  return window.chrome?.webview ?? null;
}

function normalizePayload(data) {
  if (!data) {
    return null;
  }

  if (typeof data === "string") {
    try {
      return JSON.parse(data);
    } catch (error) {
      console.warn("annotation-bridge: failed to parse payload", error);
      return null;
    }
  }

  if (typeof data === "object") {
    return data;
  }

  return null;
}

function scrollToAnnotation(annotationId) {
  if (!annotationId) {
    return false;
  }

  const id = String(annotationId);
  const selectorId = escapeSelector(id);
  const target = document.getElementById(id) ??
    document.querySelector(`[data-annotation-id="${selectorId}"]`);

  if (!target) {
    return false;
  }

  const page = target.closest(".page");
  const pageNumber = Number.parseInt(page?.dataset?.pageNumber ?? "", 10);
  const app = window.PDFViewerApplication;

  if (!Number.isNaN(pageNumber) && app?.pdfViewer) {
    app.pdfViewer.currentPageNumber = pageNumber;
  }

  target.scrollIntoView({ behavior: "smooth", block: "center" });
  target.classList.add("kw-annotation-focus");
  window.setTimeout(() => target.classList.remove("kw-annotation-focus"), 800);
  return true;
}

function escapeSelector(value) {
  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
    return CSS.escape(value);
  }

  return value.replace(/([\0-\x1F\x7F"'\\])/g, "\\$1");
}

function handleMessage(event) {
  const payload = normalizePayload(event?.data);
  if (!payload || typeof payload.type !== "string") {
    return;
  }

  switch (payload.type) {
    case MESSAGE_TYPE_SCROLL: {
      const annotationId = payload.annotationId ?? payload.id;
      scrollToAnnotation(annotationId);
      break;
    }
    default:
      break;
  }
}

function ensureBridge() {
  const chromeWebView = getChromeWebView();
  if (!chromeWebView) {
    return;
  }

  if (chromeWebView.__kwAnnotationBridgeInitialized) {
    return;
  }

  chromeWebView.addEventListener("message", handleMessage);
  chromeWebView.__kwAnnotationBridgeInitialized = true;
}

ensureBridge();
window.addEventListener("DOMContentLoaded", ensureBridge);
