// ===================================================================
// pdfjs-expose.js
// ===================================================================
// Place this file in: src/LM.App.Wpf/wwwroot/pdfjs/knowledgeworks/pdfjs-expose.js
// This exposes the pdfjsViewer namespace to window for our bridge scripts
// ===================================================================

(function () {
    'use strict';

    console.log('pdfjs-expose: Waiting for PDFViewerApplication...');

    let retryCount = 0;
    const maxRetries = 50; // Try for 10 seconds (50 * 200ms)

    function tryExposePdfjsViewer() {
        retryCount++;

        // Check if PDFViewerApplication exists
        if (typeof window.PDFViewerApplication === 'undefined') {
            if (retryCount < maxRetries) {
                setTimeout(tryExposePdfjsViewer, 200);
                return;
            }
            console.error('pdfjs-expose: PDFViewerApplication never became available!');
            return;
        }

        console.log('✓ pdfjs-expose: PDFViewerApplication found');

        // Wait for it to be initialized
        if (!window.PDFViewerApplication.initialized) {
            console.log('pdfjs-expose: Waiting for initialization...');

            window.PDFViewerApplication.initializedPromise
                .then(() => {
                    console.log('✓ pdfjs-expose: PDFViewerApplication initialized');
                    exposePdfjsViewer();
                })
                .catch((error) => {
                    console.error('pdfjs-expose: Initialization failed:', error);
                });
            return;
        }

        // Already initialized
        exposePdfjsViewer();
    }

    function exposePdfjsViewer() {
        // PDFViewerApplication has internal references to pdfjsViewer classes
        // We need to extract them and expose globally

        const app = window.PDFViewerApplication;

        // Create the pdfjsViewer namespace if it doesn't exist
        if (!window.pdfjsViewer) {
            window.pdfjsViewer = {};
            console.log('✓ pdfjs-expose: Created window.pdfjsViewer namespace');
        }

        // Try to get AnnotationEditorUIManager from the app
        if (app._annotationEditorUIManager) {
            const manager = app._annotationEditorUIManager;
            const proto = Object.getPrototypeOf(manager);
            const constructor = proto.constructor;

            window.pdfjsViewer.AnnotationEditorUIManager = constructor;
            console.log('✓ pdfjs-expose: Exposed AnnotationEditorUIManager via _annotationEditorUIManager');
        }
        // Fallback: try to find it from pdfViewer
        else if (app.pdfViewer?._annotationEditorUIManager) {
            const manager = app.pdfViewer._annotationEditorUIManager;
            const proto = Object.getPrototypeOf(manager);
            const constructor = proto.constructor;

            window.pdfjsViewer.AnnotationEditorUIManager = constructor;
            console.log('✓ pdfjs-expose: Exposed AnnotationEditorUIManager via pdfViewer');
        }
        // Another fallback: wait for the annotationeditoruimanager event
        else {
            console.log('pdfjs-expose: AnnotationEditorUIManager not found yet, listening for event...');

            if (app.eventBus) {
                app.eventBus.on('annotationeditoruimanager', (evt) => {
                    if (evt.uiManager) {
                        const proto = Object.getPrototypeOf(evt.uiManager);
                        const constructor = proto.constructor;

                        window.pdfjsViewer.AnnotationEditorUIManager = constructor;
                        console.log('✓ pdfjs-expose: Exposed AnnotationEditorUIManager via event');

                        // Notify bridge scripts that it's now available
                        window.dispatchEvent(new CustomEvent('pdfjsViewerReady'));
                    }
                });
            }
        }

        // Verify and notify
        if (window.pdfjsViewer.AnnotationEditorUIManager) {
            console.log('✅ pdfjs-expose: pdfjsViewer namespace ready!');

            // Dispatch custom event so other scripts know it's ready
            window.dispatchEvent(new CustomEvent('pdfjsViewerReady'));
        }
    }

    // Start trying immediately
    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        tryExposePdfjsViewer();
    } else {
        window.addEventListener('DOMContentLoaded', tryExposePdfjsViewer, { once: true });
    }
})();