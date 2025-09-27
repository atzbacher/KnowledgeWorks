# Tabula package compatibility notes

## Package availability
The Tabula-Sharp port is published to NuGet as the [`Tabula`](https://www.nuget.org/packages/Tabula) package.  Version 0.1.5 is the latest stable release and targets .NET 8.0 alongside earlier frameworks.  Its nuspec declares a dependency on `PdfPig` 0.1.10, which bundles the `UglyToad.PdfPig` assemblies used internally by the library.

## Impact on the current solution
The KnowledgeWorks solution already references `UglyToad.PdfPig` 1.7.0-custom-5 (plus the split `UglyToad.PdfPig.Core` and `UglyToad.PdfPig.Fonts` packages).  Because the Tabula package brings in an older `PdfPig` dependency with the same assembly identity, a direct `dotnet add package Tabula --version 0.1.5` will resolve to mismatched versions during restore/build.  Resolving this conflict requires either updating Tabula to run against the newer PdfPig assemblies or downgrading the entire workspace to the 0.1.10 dependency set.

## Recommended next steps
Before adopting Tabula, decide whether the project can migrate off the customized 1.7.x PdfPig packages.  If that is not an option, the extraction service should embed the Tabula sources and compile them against the existing PdfPig version, or else load the library in an isolated `AssemblyLoadContext` and translate results back into the current model.
