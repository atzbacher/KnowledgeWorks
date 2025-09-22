# Library drag-and-drop QA

## Prerequisites
- Launch the WPF shell and choose a workspace with at least one entry.
- Ensure the entry detail panel shows the target entry (click the row in the results grid).

## Add attachments via drag & drop
1. Open File Explorer and select one or more supported files (`.pdf`, `.doc`, `.docx`, `.ppt`, `.pptx`, `.txt`, `.md`).
2. Drag the files onto the detail panel **or** directly onto a row in the results grid.
3. Expected: the drop cursor shows a **Copy** icon, the target row becomes selected, and the attachments list refreshes with the new files.
4. Confirm the workspace now contains hashed copies under `library/` and the entry JSON lists the new attachment paths.

### Drop onto an unselected row
1. Deselect all items (e.g., press `Esc`) so the detail panel is empty.
2. Drag a supported file from File Explorer and drop it onto a different row in the results grid.
3. Expected: the drop is accepted, the row becomes selected, and the attachments list updates for that entry.


## Unsupported files
1. Drag a file with an unsupported extension (e.g., `.exe`) onto the same detail panel.
2. Expected: the drop cursor is disabled and the UI displays an informational message stating the file was skipped.
3. Verify that no new attachment is added and the workspace is unchanged.

## Duplicate handling
1. Drag a file that has already been attached (or drop the same file twice).
2. Expected: the UI skips the duplicate, reports that it was already attached, and the entry is not resaved (no timestamp change).

## Error resilience
- Temporarily make the workspace read-only (e.g., remove write permissions) and attempt another drop.
- Expected: the app reports that the save failed and the attachment list remains unchanged after the failure.

Document any deviations, including screenshots of message boxes, before marking the QA run complete.
