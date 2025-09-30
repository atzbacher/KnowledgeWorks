const PREVIEW_MESSAGE_TYPE = "highlight-created";
const PREVIEW_CANVAS_ID = "kw-highlight-render-canvas";
const processedAnnotations = new Set();

function getChromeWebView() {
  return window.chrome?.webview ?? null;
}

function ensureRenderCanvas() {
  let canvas = document.getElementById(PREVIEW_CANVAS_ID);
  if (!canvas) {
    canvas = document.createElement("canvas");
    canvas.id = PREVIEW_CANVAS_ID;
    canvas.style.display = "none";
    document.body.append(canvas);
  }
  return canvas;
}

function intersection(rectA, rectB) {
  const left = Math.max(rectA.left, rectB.left);
  const top = Math.max(rectA.top, rectB.top);
  const right = Math.min(rectA.right, rectB.right);
  const bottom = Math.min(rectA.bottom, rectB.bottom);
  const width = Math.max(0, right - left);
  const height = Math.max(0, bottom - top);
  return { left, top, width, height };
}

async function waitForAnimationFrame() {
  return new Promise(resolve => window.requestAnimationFrame(() => resolve()));
}

async function renderPageToCanvas(pageView, canvas) {
  const viewport = pageView.viewport.clone();
  const width = Math.ceil(viewport.width);
  const height = Math.ceil(viewport.height);

  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext("2d", { alpha: false });
  if (!context) {
    throw new Error("Unable to acquire 2D rendering context.");
  }

  context.save();
  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, width, height);
  context.restore();

  const task = pageView.pdfPage.render({ canvasContext: context, viewport });
  await task.promise;

  return { width, height };
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function computeCropRegion(editorRect, pageRect, canvasSize) {
  const bounds = intersection(editorRect, pageRect);
  if (bounds.width === 0 || bounds.height === 0) {
    return null;
  }

  const scaleX = canvasSize.width / pageRect.width;
  const scaleY = canvasSize.height / pageRect.height;

  const cropX = (bounds.left - pageRect.left) * scaleX;
  const cropY = (bounds.top - pageRect.top) * scaleY;
  const cropWidth = bounds.width * scaleX;
  const cropHeight = bounds.height * scaleY;

  const normalized = {
    x: (bounds.left - pageRect.left) / pageRect.width,
    y: (bounds.top - pageRect.top) / pageRect.height,
    width: bounds.width / pageRect.width,
    height: bounds.height / pageRect.height,
  };

  return {
    cropX: clamp(cropX, 0, canvasSize.width),
    cropY: clamp(cropY, 0, canvasSize.height),
    cropWidth: clamp(cropWidth, 1, canvasSize.width),
    cropHeight: clamp(cropHeight, 1, canvasSize.height),
    normalized,
  };
}

function extractPreview(canvas, cropRegion) {
  const cropCanvas = document.createElement("canvas");
  const width = Math.max(1, Math.round(cropRegion.cropWidth));
  const height = Math.max(1, Math.round(cropRegion.cropHeight));
  cropCanvas.width = width;
  cropCanvas.height = height;

  const ctx = cropCanvas.getContext("2d");
  if (!ctx) {
    throw new Error("Unable to acquire preview canvas context.");
  }

  ctx.drawImage(
    canvas,
    cropRegion.cropX,
    cropRegion.cropY,
    cropRegion.cropWidth,
    cropRegion.cropHeight,
    0,
    0,
    width,
    height,
  );

  const dataUrl = cropCanvas.toDataURL("image/png");
  const [, base64 = ""] = dataUrl.split(",", 2);

  return {
    width,
    height,
    base64,
    normalized: cropRegion.normalized,
  };
}

async function captureHighlightPreview(editor) {
  const chromeWebView = getChromeWebView();
  if (!chromeWebView) {
    return;
  }

  const annotationId = editor?.annotationElementId;
  if (!annotationId) {
    return;
  }

  const annotationKey = String(annotationId);
  if (processedAnnotations.has(annotationKey)) {
    return;
  }

  const app = window.PDFViewerApplication;
  const pageView = app?.pdfViewer?.getPageView(editor.pageIndex ?? 0);
  if (!pageView || !pageView.pdfPage || !editor?.div) {
    return;
  }

  await waitForAnimationFrame();

  const editorRect = editor.div.getBoundingClientRect();
  const pageRect = pageView.div?.getBoundingClientRect();
  if (!pageRect || editorRect.width === 0 || editorRect.height === 0) {
    return;
  }

  try {
    const renderCanvas = ensureRenderCanvas();
    const canvasSize = await renderPageToCanvas(pageView, renderCanvas);
    const cropRegion = computeCropRegion(editorRect, pageRect, canvasSize);
    if (!cropRegion) {
      return;
    }

    const preview = extractPreview(renderCanvas, cropRegion);
    if (!preview.base64) {
      return;
    }

    chromeWebView.postMessage({
      type: PREVIEW_MESSAGE_TYPE,
      annotationId: annotationKey,
      pageIndex: editor.pageIndex ?? 0,
      preview,
    });

    processedAnnotations.add(annotationKey);
  } catch (error) {
    console.error("Failed to capture highlight preview", error);
  }
}

function patchAnnotationLayer() {
  const viewerNS = window.pdfjsViewer;
  const layerProto = viewerNS?.AnnotationEditorLayer?.prototype;
  if (!layerProto || layerProto.__kwHighlightPatchApplied) {
    return;
  }

  const originalAdd = layerProto.add;
  layerProto.add = function add(editor) {
    originalAdd.call(this, editor);
    if (editor?.name === "highlightEditor") {
      queueMicrotask(() => captureHighlightPreview(editor));
    }
  };

  Object.defineProperty(layerProto, "__kwHighlightPatchApplied", {
    value: true,
    configurable: false,
    enumerable: false,
    writable: false,
  });
}

async function initialize() {
  try {
    const app = window.PDFViewerApplication;
    if (!app) {
      return;
    }

    await app.initializedPromise;
    patchAnnotationLayer();
  } catch (error) {
    console.error("Failed to initialize highlight preview bridge", error);
  }
}

if (document.readyState === "complete" || document.readyState === "interactive") {
  void initialize();
} else {
  window.addEventListener("DOMContentLoaded", () => void initialize(), { once: true });
}
