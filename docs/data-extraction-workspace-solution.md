# Data Extraction Workspace – Proposed Solution Blueprint

## 1. Goals
- Deliver reliable, reviewer-driven extraction for PDF evidence with full provenance.
- Combine deterministic preprocessing with manual curation to maximize accuracy.
- Persist every mutation via changelog hooks annotated with the Windows username.
- Produce reusable assets (CSV, thumbnails, high-resolution crops) for downstream exports and presentations.

## 2. System Architecture Overview
1. **Asset Preparation Layer**
   - Deterministic services (PdfPig text parsing, PdfPig rasterization, Tabula-based table extraction, optional Tesseract OCR for image-only regions).
   - Writes normalized artifacts (full-page text, page PNGs, extracted tables) into staging storage under the entry workspace layout.
2. **Staging Storage Schema**
   - `entries/<entryId>/source/` → raw PDFs, parser outputs, page images.
   - `entries/<entryId>/extraction/` → curated tables (`*.csv`), figures (`*.png`/`*.json` metadata), OCR transcripts.
   - `entries/<entryId>/hooks/changelog.json` → append-only change history capturing author, timestamp, action, and affected asset identifiers.
3. **Application Layer (WPF)**
   - **Staging Editor** hosts metadata/tables/figures/population/review tabs and exposes a **Data Extraction…** workspace launcher when PDF-backed evidence is available.
   - **Data Extraction Workspace** provides PDF viewing, region selection, asset tagging, OCR invocation, and dictionary-driven field suggestions.
4. **Commit & Export Layer**
   - `DataExtractionHook` serialization pipeline records curated assets, provenance (page numbers, coordinates, hashes), OCR transcripts, and dictionary-based structured fields.
   - Export services (Excel/Word/PPT) consume hook payloads to render tables/figures with citations.

## 3. Workflow Walkthrough
1. **Staging Entry Selection**
   - Reviewer opens staging editor and clicks **Data Extraction…** for a PDF-backed entry.
   - Command verifies prerequisites (PDF path, pre-generated page thumbnails) and initializes the workspace view model.
2. **Workspace Layout**
   - Left pane: multi-page PDF viewer with zoom/pan, rendered via SkiaSharp or PdfPig rasterization.
   - Right pane: tabbed detail panel.
     - **Tab 1 – Tables & Figures**
       - Toolbar buttons: *Add Table*, *Add Figure*.
       - Rectangle selector overlays with snap-to-grid assistance and page navigation.
       - Asset editor fields: Name (e.g., "Figure 1A – KM mortality"), Type, Tags, Column Count hint, Page, Source hash, Confidence score.
       - Actions: Run OCR (Tesseract) for image-only regions, Generate CSV skeleton, Capture high-resolution crop.
     - **Tab 2 – Study Classification**
       - Controls for study design (RCT, retrospective, meta-analysis), center type (single vs multicenter), sample sizes.
       - Dictionary-backed dropdowns enabling consistent metadata tagging.
     - **Tab 3 – Domain Dictionaries**
       - Key/value grid for baseline and outcome mappings using canonical identifiers (`Baseline.Age`, `Baseline.Comorbidities.Rhythm.LBBB`, etc.).
       - Auto-suggestion panel surfaces dictionary matches from deterministic preprocessing; reviewers accept/modify values.
     - **Tab 4 – Review & Commit**
       - Summaries of curated assets, validation warnings, provenance snapshots, and changelog preview.
       - Buttons: *Save Draft* (updates staging item only) and *Commit Extraction* (writes `DataExtractionHook`, triggers changelog write, recalculates hashes).
3. **Asset Generation**
   - Saving a table region triggers deterministic extraction pipeline:
     1. Crop region using PdfPig rasterization for preview and high-res PNG export.
     2. For text-backed PDFs, clip text by geometry; fall back to Tesseract OCR for image-only sections.
     3. Run Tabula/Camelot to infer table structure; user can adjust column count hints before regeneration.
     4. Persist CSV to `extraction/tables/<assetId>.csv` and metadata JSON referencing page, coordinates, hash, OCR transcript path.
   - Figure capture stores thumbnail (`*_thumb.png`) and original-resolution crop (`*_full.png`), along with tags for reuse in presentations.
4. **Changelog Hook Updates**
   - Every save/commit operation appends to `hooks/changelog.json` with fields: `timestamp`, `windowsUser`, `action`, `assetIds`, `notes`.
   - Hook writer obtains Windows username via `Environment.UserName` (or stubbed for tests) ensuring auditability.
5. **Export Integration**
   - Excel exporter reads curated tables (`csvPath`) and dictionary annotations to create structured baseline/outcome sheets.
   - Word exporter embeds tables/figures with captions and citations (DOI/PMID, page, hash).
   - PowerPoint exporter assembles slides with figure thumbnails and quick links to high-resolution assets.

## 4. Component Responsibilities
| Component | Responsibility |
|-----------|----------------|
| `PdfPageImageService` | Deterministically rasterize pages, cache PNGs, supply crops for figures/tables. |
| `PdfRegionTextExtractor` | Clip embedded text via PdfPig; fallback to OCR provider. |
| `OcrProvider` | Wrap Tesseract CLI/engine with deterministic configuration, caching transcripts by hash. |
| `TableStructureExtractor` | Invoke Tabula/Camelot with column hints; produce normalized CSV + diagnostics. |
| `DataExtractionWorkspaceViewModel` | Manage assets, commands, validation, synchronization with staging item. |
| `DataExtractionRegionViewModel` | Track page, coordinates, zoom state, selection handles. |
| `DictionarySuggestionService` | Map text/OCR tokens to canonical keys using configuration dictionaries. |
| `EntryChangeLogService` | Append changelog entries referencing Windows username for every mutation. |
| `DataExtractionCommitBuilder` | Transform staged workspace state into `DataExtractionHook` payloads with provenance. |

## 5. Implementation Phases
1. **Foundation (Sprint 1)**
   - Build asset preparation services (page rasterization, text clipping, OCR wrapper).
   - Define staging storage layout, region metadata models, and changelog schema updates.
   - Extend `DataExtractionHook` models to store regions, OCR transcripts, asset hashes.
2. **Workspace MVP (Sprint 2)**
   - Implement WPF workspace window with PDF viewer, region selection, asset list management.
   - Wire workspace launch from staging editor via dialog service and command gating.
   - Support table/figure creation, basic metadata editing, and draft save (no commit yet).
3. **Extraction Automation (Sprint 3)**
   - Integrate Tabula/Camelot and OCR flows triggered from workspace actions.
   - Generate CSV thumbnails, high-res crops, and attach dictionary hints.
   - Persist outputs to staging storage, update changelog hook.
4. **Commit & Export Integration (Sprint 4)**
   - Finalize `DataExtractionCommitBuilder` to serialize curated assets with provenance.
   - Update staging tabs and exporters to load manual assets, ensuring backward compatibility not required.
   - Add automated tests covering commit, changelog writes, and exporter consumption.
5. **Polish & QA (Sprint 5)**
   - Implement validation (missing captions, conflicting dictionary entries).
   - Provide undo/redo stack and workspace autosave.
   - Update documentation (`docs/`) and deliver training outline.

## 6. Open Questions & Risks
- **OCR Accuracy**: Define deterministic configuration for Tesseract (language packs, DPI) and fallback strategy when OCR confidence is low.
- **Performance**: Large PDFs may require caching/range loading; consider async page rendering.
- **Dictionary Maintenance**: Determine governance for taxonomy files (versioning, reviewer feedback loop).
- **Export Parity**: Ensure exporters gracefully handle assets missing OCR or dictionary fields.
- **Security/Compliance**: Verify temporary OCR files are cleaned and sensitive data is stored within staging entry scope only.

## 7. Next Actions
1. Confirm availability/licensing for Tabula/Camelot and Tesseract in deployment environments.
2. Finalize dictionary syntax and storage location (e.g., `config/dictionaries/*.json`).
3. Create detailed technical specifications for new services (`PdfRegionTextExtractor`, `EntryChangeLogService`).
4. Spike workspace PDF viewer performance with representative PDFs.
5. Align QA plan with deterministic extraction checklist and update `docs/library-extraction-exports.md` after implementation.
