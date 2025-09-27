# TabulaSharp integration notes

The solution now ships the lightweight `TabulaSharp` heuristics as an in-repo project (`src/TabulaSharp/TabulaSharp.csproj`) rather than a pre-built NuGet package. This keeps the dependency surface human-auditable and avoids distributing binary artifacts alongside the source tree. The code consumes `PdfPig` geometry to create normalized row/column sets while remaining dependency-light and self-contained for offline restore scenarios.

TabulaSharp’s extractor replaces the former Tabula-native pipeline: it collects PdfPig tokens, groups them into logical lines, and applies heuristic clustering to yield structured tables. The infrastructure layer continues to rasterize cropped table images through `Docnet.Core` and `SkiaSharp`, keeping staging artifacts aligned with downstream WPF requirements.

Projects that previously referenced the Tabula package (or split `UglyToad.PdfPig.Core`/`Fonts` packages) now add a project reference to `TabulaSharp`. Central package management continues to pin the remaining third-party dependencies (PdfPig, Docnet.Core, etc.) to compatible versions across the solution.
