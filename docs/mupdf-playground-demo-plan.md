# MuPDF playground demo tasks

The existing playground already exposes a number of MuPDF-centric workflows:

- Render arbitrary pages from the selected entry's PDF with adjustable zoom and annotation overlays. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L361-L460】
- Allow users to highlight regions, persist notes, and push the interactions into the changelog hook for traceability. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L193-L354】【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L467-L518】
- Export the currently displayed page or extract its structured text via MuPDF APIs. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L566-L653】

To turn this into a demo that exercises the feature set end-to-end:

1. **Curate a representative PDF library entry.** Ensure it contains a mix of portrait, landscape, and rotated pages so the rendering pipeline and clipping behaviour can be showcased.
2. **Script the loading flow.** Use the existing `InitializeAsync` path to highlight prerequisites (entry ID, attachment resolution) and show the new "no clipping" behaviour on rotated pages. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L121-L189】
3. **Demonstrate annotation capture.** Toggle selection mode, drag regions, enter notes, and surface the generated changelog events in telemetry/logging. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L193-L354】【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L467-L518】
4. **Showcase export and text extraction.** Invoke the export and copy commands, saving artifacts to disk or the clipboard for validation. 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L566-L653】
5. **Summarise MuPDF integration points.** Close by reviewing how rendering, annotations, and text extraction map onto `MuPDFDocument` and related primitives for stakeholders interested in extending support (e.g., multi-threaded rendering, OCR). 【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L136-L147】【F:src/LM.App.Wpf/ViewModels/Library/MuPdfPlaygroundViewModel.cs†L566-L653】

This checklist can be adapted into a narrated walkthrough or automated smoke test to validate the playground before releases.
