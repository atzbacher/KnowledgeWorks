# Data Storage

_Back to [overview](README.md)_ • [Architecture](architecture.md) • [Contracts](service-contracts.md) • [UI](ui-workflow.md)

## Filesystem layout
```
%WORKSPACE%/
  extraction/
    aa/bb/<region-hash>.json      # Region descriptor (see schema below)
    aa/bb/<region-hash>.png       # Cropped image
    aa/bb/<region-hash>.txt       # OCR text (UTF-8)
    aa/bb/<region-hash>.ppmx      # Optional Office-friendly package
```
- `aa/bb/` path segments are derived from the first four hex characters of the region hash (SHA-256 of source SHA + coordinates).
- JSON descriptor includes references back to the source entry, original page, selection bounds, tags, and exporter metadata.
- Assets are stored alongside doc-specific subfolders when per-document grouping is enabled (`UsePerEntryFolders`).

## SQLite schema
Region descriptors are indexed in the local SQLite database alongside other KnowledgeWorks entities. Add the following tables:

```sql
CREATE TABLE IF NOT EXISTS region_descriptor (
    region_hash TEXT PRIMARY KEY,
    entry_hub_id TEXT NOT NULL,
    source_rel_path TEXT NOT NULL,
    page_number INTEGER,
    bounds TEXT NOT NULL,          -- JSON array [x, y, width, height]
    ocr_text TEXT,
    tags TEXT,
    notes TEXT,
    created_utc TEXT NOT NULL,
    last_export_status TEXT NOT NULL,
    office_package_path TEXT,
    extra_metadata TEXT
);

CREATE INDEX IF NOT EXISTS idx_region_descriptor_entry ON region_descriptor(entry_hub_id);
CREATE VIRTUAL TABLE IF NOT EXISTS region_descriptor_fts USING fts5(region_hash, ocr_text, content='region_descriptor', content_rowid='rowid');
```

- `RegionDescriptor` records map to this table. Repository methods ensure `region_descriptor_fts` stays synchronized (trigger-based or manual refresh).
- `source_rel_path` uses the same relative path semantics as `EntryHub.Primary.RelPath`.
- Use `HubSpokeStore` migrations to create the schema during workspace initialization.

## Retention & cleanup
- Default retention: keep all extraction assets until the parent entry is removed. Deleting an entry triggers a cascading cleanup routine.
- Provide a "Compact extraction storage" command that purges orphaned descriptors and prunes failed exports older than 14 days.
- Background cleanup jobs run during idle windows only and respect offline disk quotas.
