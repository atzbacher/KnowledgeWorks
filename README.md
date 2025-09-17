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
