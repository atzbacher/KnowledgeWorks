# Service Contracts

_Back to [overview](README.md)_ • [Architecture](architecture.md) • [UI](ui-workflow.md) • [Storage](data-storage.md)

## Core interfaces
| Contract | Responsibilities | Notes |
| --- | --- | --- |
| `IRegionExporter` | Accepts `RegionSelection` + source `EntryHub` and emits a `RegionExportResult` (paths, OCR text, checksum). | Implementations must be pure and cancellable; all heavy IO occurs off the UI thread. |
| `IRegionPreviewService` | Produces lightweight preview bitmaps for quick redraws and thumbnail lists. | Can cache to `%LocalAppData%/KnowledgeWorks/cache/extractor` but must auto-evict when offline disk quotas are hit. |
| `IExtractionRepository` | Persists region descriptors (`RegionDescriptor` records) and exposes query APIs for UI history + Office add-in lookups. | Backed by SQLite; ensures idempotent upsert keyed by `(EntryHubId, RegionHash)`. |
| `IHubQueue` | Existing hub ingestion queue used to push `SpokeIndexContribution` jobs. | Visual extractor adds a `Extraction` job type with throttled concurrency. |
| `IExtractionPostProcessor` (optional) | Chainable hooks invoked after export completes. | Used for ML tagging, color palette detection, etc.; must complete within 2s or run via background job. |

## Commands & events
- `ExportRegionCommand` (UI → services) contains: region geometry, annotation text, tags, requested exporters, and add-in destinations.
- `RegionExportCompleted` event surfaces to the UI and to `HubSpokeStore`. Carries references to stored assets plus telemetry metadata (duration, exporter kind).
- `RegionExportFailed` event includes sanitized exception info; the UI will display actionable hints and log to local diagnostic files.

## Dependency injection
- Register services in `LM.App.Wpf` composition root using scoped lifetimes per window instance.
- Exporters may depend on `IContentExtractor`, `IPdfRenderer`, or other infrastructure packages already shipped with KnowledgeWorks.
- Each contract must have a mock/stub registered in test projects (`LM.App.Wpf.Tests`, `LM.Infrastructure.Tests`) to keep integration tests offline and deterministic.
