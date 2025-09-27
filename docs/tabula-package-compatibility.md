# Tabula integration notes

The solution now uses the official [`Tabula`](https://www.nuget.org/packages/Tabula) 0.1.5 package alongside `PdfPig` 0.1.10 to power table detection. Tabula ships a dependency on the monolithic `UglyToad.PdfPig` assembly; the repository pins the same version centrally so restore proceeds without assembly conflicts. No custom fork is required.

Tabula’s `SimpleNurminenDetectionAlgorithm` and extraction algorithms run during the preprocessing phase to identify table regions, emit CSV snapshots, and capture page-relative metadata. The infrastructure layer supplements Tabula by rendering cropped table images through `Docnet.Core` and `SkiaSharp`, which keeps the staging workspace aligned with downstream WPF requirements.

Projects that previously referenced the split `UglyToad.PdfPig.Core`/`Fonts` packages should remove those dependencies in favor of the central `PdfPig` meta-package. All other consumers continue using the shared assembly provided by central package management.
