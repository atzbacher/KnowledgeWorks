# Office Add-in Slice

_Back to [overview](README.md)_ • [Architecture](architecture.md) • [Contracts](service-contracts.md) • [UI](ui-workflow.md)

## Goals
- Provide a minimal ribbon for PowerPoint/Word that fetches recent region extractions without round-tripping through cloud services.
- Allow insertion of cropped imagery + citations into the active document, preserving attribution back to the KnowledgeWorks entry.
- Keep the add-in deployable via the existing offline installer bundle.

## Add-in architecture
- **Host shim**: `ExtractorAddInShim` is a thin .NET component loaded by Office; it communicates with the WPF app over named pipes (`ExtractorPipeServer`).
- **Commands**:
  - "Insert latest extraction" – pulls the most recent `RegionDescriptor` for the active workspace user.
  - "Browse workspace" – opens a lightweight webview listing extraction history, powered by the same `IExtractionRepository` APIs.
  - "Refresh connection" – re-establishes the pipe if the desktop app restarted.
- **Data contract**: `OfficeInsertionPayload` includes image bytes, OCR summary, citation string, and deep link back to KnowledgeWorks.

## Offline considerations
- The add-in never makes HTTP requests; all communication stays on localhost pipes secured with mutual authentication tokens.
- Bundled assets (icons, manifests) are embedded so the installer can register the add-in without internet access.
- Telemetry events queue locally and piggyback on the desktop app's diagnostics flush when the user exports logs manually.

## Deployment
- Update the installer script to drop the manifest into `%ProgramFiles%\KnowledgeWorks\OfficeAddin\` and register with `reg add` commands executed with admin privileges.
- Provide MSI transform samples for enterprise deployment referencing the same manifest location.
- Document manual fallback installation steps in the IT admin guide.
