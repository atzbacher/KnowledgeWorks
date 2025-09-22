# KnowledgeWorks (patched)

Local-first knowledge artifact manager + awareness feeds with a shared workspace and a local SQLite FTS index.

- WPF app targets `net9.0-windows`.
- Central Package Management is enabled (`Directory.Packages.props`).
- SQLite native bundle is pinned (`SQLitePCLRaw.bundle_e_sqlite3`).

Build:
```powershell
dotnet restore
dotnet build -c Debug
```
Run:
```powershell
dotnet run --project src\LM.App.Wpf\LM.App.Wpf.csproj -c Debug
```

## Feature documentation

- Visual/Data Extraction feature specifications live under [`docs/visual-extractor/`](docs/visual-extractor/). Start with the [overview](docs/visual-extractor/README.md) for architecture, service contracts, UI flows, storage, and add-in integration guidance.
