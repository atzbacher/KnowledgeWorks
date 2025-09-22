# UI Workflow

_Back to [overview](README.md)_ • [Architecture](architecture.md) • [Contracts](service-contracts.md) • [Storage](data-storage.md)

## Entry points
1. **Command palette** – `ExtractWindow` can be opened from the library grid via `Ctrl+Shift+X`. The command passes the selected `EntryHubId` into the window factory.
2. **Context menu** – right-clicking a file in `LibraryViewControl` exposes "Extract visual slice"; this persists the last-used export preset.
3. **Drag/drop** – dropping an image/PDF onto the extractor dock automatically opens `ExtractWindow` with the dropped file as context.

## Window layout
- **Region canvas** (`RegionCanvasControl`): supports rectangle and freeform lasso tools, multi-selection, zoom, and panning. Keyboard shortcuts mirror PowerPoint (e.g., `Ctrl+=` zoom in).
- **Metadata sidebar**: binds to `RegionDescriptorViewModel` to capture title, notes, tags, target board, and optional follow-up tasks.
- **Export rail**: lists configured exporters (image, text, Office) with inline status icons and retry buttons.

## Interaction flow
1. User chooses a selection tool and marks a region; a floating toolbar shows pixel dimensions and quick actions.
2. On confirm (`Enter` or "Add to queue"), the selection is captured into a `RegionSelection` model and appended to the pending exports list.
3. The coordinator kicks off export tasks sequentially (default) or in parallel (if `EnableParallelExports` flag). Progress updates animate in the export rail.
4. Once an export completes, the UI reveals a toast with shortcuts:
   - "Copy rich snippet" (puts combined image + markdown summary onto the clipboard).
   - "Open in workspace" (opens the `extraction/` folder via `WorkspaceLayout`).
   - "Send to Office" (invokes the add-in bridge when the Office host is connected).
5. Errors highlight the affected exporter, show sanitized error text, and surface a "Report with diagnostics" option that collects log excerpts without attaching document bits.

## Accessibility
- All controls have keyboard equivalents; narrations rely on `AutomationProperties.Name` bindings.
- High-contrast theme uses app-level resource dictionaries and ensures selection handles meet WCAG contrast ratios.
- Zoom controls support mouse wheel + `Ctrl` and trackpad gestures.

## Persistence of state
- The last five extraction sessions are serialized via `IExtractionRepository` and can be reloaded through the "Recent extractions" dropdown.
- UI preferences (tool choice, snap-to-grid) persist via existing settings infrastructure in `LM.App.Wpf`.
