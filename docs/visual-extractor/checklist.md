# MVP Checklist

_Back to [overview](README.md)_ • [Architecture](architecture.md) • [Contracts](service-contracts.md) • [UI](ui-workflow.md) • [Storage](data-storage.md)

## ✅ UI wiring (`ExtractWindow`)
- [ ] `ExtractWindow` launched from library context menu and shortcut; passes `EntryHubId` into `ExtractWindowViewModel`.
- [ ] Region selection tools (rectangle + lasso) implemented in `RegionCanvasControl`; keyboard shortcuts documented in in-app help.
- [ ] Export rail binds to observable collection of `RegionExportItemViewModel` with progress + retry support.
- Acceptance: manual smoke test can select a region, enter metadata, and trigger a no-op exporter without crashing.

## ✅ Exporter implementation (`IRegionExporter`)
- [ ] Default `CompositeRegionExporter` wires PDF + PPTX support by delegating to `PdfRegionExporter` and `PptRegionExporter`.
- [ ] Exports emit PNG + OCR text into `WorkspaceLayout.ExtractionRoot(...)` using deterministic region hash filenames.
- [ ] Errors bubble through `RegionExportFailed` with sanitized message and diagnostic correlation ID.
- Acceptance: running exporter against sample PDF generates descriptor JSON and assets; rerunning with same region is idempotent.

## ✅ Index creation (`HubSpokeStore`, SQLite)
- [ ] `region_descriptor` schema created via workspace migration; includes FTS table as in [data storage](data-storage.md).
- [ ] `HubSpokeStore` ingests new descriptors and publishes `SpokeIndexContribution` with OCR text + tags.
- [ ] Search UI surfaces extracted regions when querying by OCR text.
- Acceptance: integration test seeds descriptors and verifies FTS query returns expected `EntryHubId` references.

## ✅ Office add-in slice
- [ ] `ExtractorAddInShim` registers ribbon buttons (insert, browse, refresh) and communicates over named pipes.
- [ ] "Insert latest extraction" command pulls most recent `RegionDescriptor` and pastes slide-friendly layout into PowerPoint.
- [ ] Add-in handles offline state gracefully (status badge + retry) without blocking host UI.
- Acceptance: manual test inserts extraction into offline PowerPoint session with KnowledgeWorks running.

## ✅ Testing & QA
- [ ] Unit tests cover `VisualExtractionCoordinator`, `CompositeRegionExporter`, and `IExtractionRepository` migration logic.
- [ ] UI automation scenario verifies selection + export happy path using `ExtractWindow` test harness.
- [ ] Add-in integration test stubs Office host and validates named pipe payload contract.
- Acceptance: CI job `dotnet test` passes with new suites; offline smoke script exercises exporter + add-in handshake.
