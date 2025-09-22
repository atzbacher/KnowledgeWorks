# Architecture

_Back to [overview](README.md)_ • [Contracts](service-contracts.md) • [UI](ui-workflow.md) • [Storage](data-storage.md) • [Office add-in](office-addin.md)

## High-level layout
- **Presentation tier**: `ExtractWindow` (WPF) hosts the region canvas, metadata sidebar, and exporter progress rail. It binds to `ExtractWindowViewModel`, which orchestrates selection state, preview rendering, and command routing.
- **Application services**: `VisualExtractionCoordinator` mediates between UI interactions and backend services, injecting `IRegionExporter`, `IImageCache`, `IHubQueue`, and the `IClock` for repeatable timestamps.
- **Infrastructure**: Exporters serialize payloads to `WorkspaceLayout.ExtractionRoot(...)`, trigger SQLite index updates through `HubSpokeStore`, and push metadata into `EntryHub.DataExtraction` hooks for downstream spokes.

## Data flow
1. User selects a region in the viewport, generating a `RegionSelection` model with bounding boxes, zoom level, and annotations.
2. `VisualExtractionCoordinator` issues an `ExportRegionCommand` to the configured `IRegionExporter` implementation (e.g., `PptRegionExporter`, `PdfRegionExporter`).
3. Exporter writes:
   - Cropped bitmap (PNG/JPEG) with deterministic filename keyed by SHA-256 of the source file + coordinates.
   - OCR text + metadata JSON capturing provenance, tags, and `EntryHubId`.
   - Optional rich data (vector markup, color palette) for Office add-in experiences.
4. Completion is reported to the coordinator, which updates the UI state, enqueues a `SpokeIndexContribution` for re-indexing, and raises telemetry events (local only).

## Background jobs
- Batch extraction jobs can be queued through the existing `IBackgroundJobDispatcher`; each job references a persisted `RegionExtractionRequest` row.
- Failed exports rehydrate into the UI by reading `extraction/*.json` descriptors and highlighting incomplete steps.

## Extensibility
- Additional exporters implement `IRegionExporter` and register via DI; the coordinator resolves them based on the `EntryHub.Primary` MIME type.
- Hooks for plugins: `IExtractionPostProcessor` contracts can decorate exported assets (e.g., run ML tagging) provided they respect offline execution constraints.
