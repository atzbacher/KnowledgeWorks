# Library PDF Annotation Workflow

The Library tab now exposes an explicit PDF annotation workflow so QA can
validate overlay synchronization.

## Opening the viewer

1. Select an entry whose primary document is a PDF.
2. Use the **Open in PDF viewer** row action (context menu or toolbar button).
3. KnowledgeWorks resolves the workspace-relative PDF path, normalizes the
   SHA-256 hash, and launches the in-app viewer.
4. The viewer is pre-configured with the entry identifier and PDF hash so that
   annotation changes write to both the entry changelog and the shared PDF hook
   (under `entries/<hash>/hooks/`).

If the viewer cannot locate the PDF file or compute the hash, the UI surfaces a
warning dialog and skips launch so testers can correct the workspace data.

## Storage layout

* Annotations are persisted to `entries/<hash>/hooks/pdf_annotations.json`.
* Overlay payloads now live alongside the PDF (for example, `library/.../document.overlay.json`).
* A debug snapshot of the raw overlay is stored under `debug/<pdf-hash>.debug.json` for troubleshooting.
* Both the entry (`entries/<entryId>/hooks/changelog.json`) and PDF hash
  (`entries/<hash>/hooks/changelog.json`) changelog hooks receive events tagged
  with the current Windows username whenever annotations are saved.

## QA checklist

* Confirm that opening a PDF populates the viewer and displays the hash in the
  toolbar.
* Add or edit an annotation, then verify that the corresponding changelog files
  include a `pdf-annotations-updated` event stamped with your username.
* Run the overlay reader (or inspect the JSON) to confirm the overlay path is
  accessible for downstream sync tooling.
