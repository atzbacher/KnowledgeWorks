# Deterministic Evidence Extraction – Task List

## 1. Repository & Environment Readiness
- [ ] Validate dotnet 8.0+ SDK availability in CI/local dev containers; document install steps in engineering handbook.
- [ ] Add solution-level build pipeline gate for new hub/spoke components (build + unit tests).
- [ ] Introduce feature flag/config toggle for staging editor enhancements (default: disabled).

## 2. Asset Ingestion Spokes
- [ ] Extend `ArticleHook` ingestion to capture raw PDF assets, PubMed XML, and CT.gov XML in staging storage.
- [ ] Implement PDF parsing pipeline (sections, captions) leveraging deterministic parsers (PdfPig/Textual structure).
- [ ] Integrate TabulaSharp heuristics for table-to-CSV extraction with checksum logging.
- [ ] Add page-image extraction service (PdfPig) that stores per-page PNGs with hash metadata.
- [ ] Update `TrialHook` to normalize trial registry data schemas and link to article metadata.

## 3. Staging Storage & Provenance
- [ ] Define staging storage schema for entries (PDF, parsed text, tables, figures, metadata).
- [ ] Ensure every stored asset includes DOI/PMID, page number, and SHA-256 hash.
- [ ] Implement changelog hook updates for staging entries on every mutation (creator = Windows username).

## 4. StagingEditorWindow Enhancements
- [ ] Add tabbed navigation scaffold (Metadata, Tables, Figures, Endpoints, Population, Review).
- [ ] Metadata tab: auto-populate from PubMed/CT.gov; enable manual overrides with audit trail.
- [ ] Tables tab: render parsed tables in editable grids; surface classifier confidence + manual override controls.
- [ ] Figures tab: display thumbnails, allow open-in-external digitizer, accept CSV uploads.
- [ ] Endpoints tab: provide controlled vocabulary dropdowns and timepoint/unit selectors.
- [ ] Population tab: pre-fill baseline characteristics; allow editing with validation (numeric ranges, % sums).
- [ ] Review tab: summarize captured data; offer “Commit Extraction” and “Commit Metadata Only” actions.

## 5. Deterministic Pre-Population Services
- [ ] Build table type classifier using deterministic dictionaries (baseline vs outcomes vs procedural).
- [ ] Implement row mapping dictionaries for baseline/outcome signals (age, hypertension, mortality, stroke, PVL, etc.).
- [ ] Create regex-based numeric extractors (n, %, HR, CI) with unit tests across representative samples.
- [ ] Parse grouped column headers to derive intervention names and population sizes (`n=...`).
- [ ] Map outcome labels to structured endpoint models (name, timepoint, comparator).
- [ ] Surface pre-populated suggestions in UI with accept/reject toggles and provenance display.

## 6. DataExtractionHook Commit Path
- [ ] Define deterministic serialization schema for populations, interventions, endpoints, and figures.
- [ ] Implement “Commit Extraction” workflow writing to `DataExtractionHook`, including provenance payloads (page/hash).
- [ ] Support “Metadata Only” commit path that stores bibliographic data without structured evidence.
- [ ] Unit-test hook writer to guarantee changelog updates and checksum persistence.

## 7. KnowledgeGraphHub Integration
- [ ] Expand graph schema to include populations, interventions, endpoints, and asset nodes with relationships.
- [ ] Implement ingestion job syncing `DataExtractionHook` outputs into the knowledge graph.
- [ ] Add query interfaces for common evidence synthesis questions (mortality comparisons, baseline characteristics, KM overlays).
- [ ] Create regression tests ensuring graph updates remain deterministic and idempotent.

## 8. Reuse & Export Workflows
- [ ] Implement export service for PowerPoint (table/figure + citation footnote).
- [ ] Implement Excel export bundling selected endpoints/baseline data into CSV/XLSX.
- [ ] Implement Word export producing formatted tables with provenance metadata.
- [ ] Ensure each export embeds DOI/PMID, page reference, and asset hash.

## 9. Operations & QA
- [ ] Seed staging environment with sample PDFs/XMLs for UAT.
- [ ] Document reviewer workflow in `docs/` and conduct training session outline.
- [ ] Define telemetry/logging for parsing success rates and manual overrides.
- [ ] Establish automated nightly job to re-verify hashes and detect asset drift.
- [ ] Plan for future AI suggestion engine integration hooks (non-blocking placeholder interfaces).
