# Visual Extraction Feature Overview

The Visual/Data Extraction feature lets researchers capture high-signal slices from rich documents and wire them into KnowledgeWorks' local-first knowledge graph.
It provides an ergonomic front-end for selecting regions, annotating context, exporting cropped imagery plus searchable text, and propagating the captured slice into indexes, workspace storage, and Office surfaces.

## Purpose
- Give WPF operators a dedicated surface (`ExtractWindow`) to triage slides, PDFs, and screenshots alongside their original entries.
- Preserve the provenance of each extracted region so downstream spokes (e.g., lit search, review surfaces) can cite the original file and coordinates.
- Enable multi-step workflows that stay offline-first by leaning on existing local services (`WorkspaceLayout`, SQLite FTS, background job runners).

## Core Capabilities
- **Region capture pipeline** – selection UX drives an `IRegionExporter` implementation that outputs raster crops, OCR text, and semantic tags.
- **Index propagation** – exported slices are normalized into hub `EntryHub` hooks, queued for `HubSpokeStore` ingestion, and re-indexed via `SpokeIndexContribution` contracts.
- **Contextual annotations** – optional notes and linkage to `Review` modules are persisted with the region metadata record.
- **Office add-in bridge** – ships a trimmed set of commands for the Office add-in slice so users can pull extractions into slide decks without leaving their document.

## Offline Constraints
- All extraction, OCR, and packaging services MUST run locally; any ML/OCR dependencies must be redistributed as part of the desktop installer.
- The pipeline never transmits content off-device. Logging omits document payloads and only stores hashed identifiers plus user actions.
- Background jobs rely on the existing scheduler primitives; introduce no new always-on services or cloud callbacks.

## Navigation
- [System architecture](architecture.md)
- [Service contracts](service-contracts.md)
- [UI workflow](ui-workflow.md)
- [Data storage model](data-storage.md)
- [Office add-in slice](office-addin.md)
- [MVP checklist](checklist.md)
