# Knowledge Graph Hub

The knowledge graph hub materializes structured data captured in `DataExtraction` hooks into a SQLite graph that supports cross-entry insights.

## Responsibilities

- Load `hub.json` metadata to discover the `data_extraction` hook for each entry.
- Parse the `DataExtractionHook` payload and project paper, population, intervention, and endpoint nodes.
- Persist relationships such as population assignments, intervention edges, and endpoint readouts in `extraction/knowledge_graph.db`.
- Expose query helpers for mortality comparisons, Kaplan–Meier overlays, and baseline characteristic lookups.

## Query surface

| Method | Description |
| --- | --- |
| `GetMortalityComparisonsAsync` | Returns endpoint readouts flagged as mortality along with the source and comparator interventions. |
| `GetKaplanMeierOverlaysAsync` | Retrieves Kaplan–Meier curves for an entry (optionally filtered to a single endpoint) so callers can overlay survival estimates. |
| `SearchBaselineCharacteristicsAsync` | Performs a case-insensitive search over stored baseline characteristics, returning the matching populations. |
| `GetEntryOverviewAsync` | Provides a snapshot of the nodes and edges currently persisted for an entry, useful for diagnostics and visualizations. |

All APIs accept a `CancellationToken` and rely on the shared workspace service for path resolution. Graph initialization creates the backing SQLite database on demand under the workspace extraction folder.

## Notifications

`HubSpokeStore` invokes `IKnowledgeGraphHub.RefreshEntryAsync` after every save so the graph reflects new or updated hooks immediately. When an extraction file is missing, the hub prunes existing nodes to avoid stale results.

## Testing

`KnowledgeGraphHubTests` covers ingestion of sample extraction hooks, query projections, and the integration point with `HubSpokeStore`. Use `dotnet test` to validate changes before committing.
