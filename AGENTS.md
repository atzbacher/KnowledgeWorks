# KnowledgeWorks Agent Playbook

## Scope
These instructions apply to the entire repository unless a more specific `AGENTS.md` is added in a subdirectory.

## Project overview
- WPF desktop client in `src/LM.App.Wpf` (targets `net9.0-windows`) built on MVVM.
- Shared domain logic and abstractions live in `src/LM.Core`, infrastructure/services in `src/LM.Infrastructure`, and hub-and-spoke orchestration in `src/LM.HubSpoke`.
- Automated tests exist for core logic, infrastructure, hub/spoke orchestration, and the WPF layer (`*.Tests` projects).
- Public surface area is tracked with `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`; builds aggregate them into `PublicAPI/CombinedPublicAPI.txt`.

## Workflow expectations
1. Search for additional `AGENTS.md` files whenever you enter a new folder tree.
2. Keep files human-readable: prefer small, focused classes and split responsibilities across partial classes or helper types when it makes architectural sense.
3. Prefer incremental, well-scoped changes; avoid large refactors unless explicitly requested.
4. When adjusting dependency graphs, update the appropriate composition module under `src/LM.App.Wpf/Composition` so DI stays consistent.
5. Document noteworthy UX or QA impacts in `docs/` if you change workflows covered there.

## Coding guidelines
- Follow existing style: block-scoped namespaces, 4-space indentation, nullable reference types enabled, and `ArgumentNullException.ThrowIfNull`/guard clauses for injected dependencies.
- For asynchronous work, expose `Task`-returning members, respect `CancellationToken` parameters, and call `.ConfigureAwait(false)` in library/infrastructure code paths.
- Keep view logic inside view models/commands (MVVM). Avoid adding non-trivial business logic to WPF code-behind files.
- Favor `sealed` classes when you do not intend inheritance and keep members `internal` unless they must be public.
- Before expanding a file beyond a few hundred lines, consider extracting cohesive regions into new files or partials.
- Adhere to best practices for large C# solutions: clear separation of concerns, DI-friendly constructors, unit-testable methods, and no static mutable state.

## Public API management
- Any change that alters a public or `internal` API consumed across projects must update `PublicAPI.Unshipped.txt` in the affected project.
- After modifying API files, run `dotnet build` to regenerate `PublicAPI/CombinedPublicAPI.txt` and include relevant updates in your commit (never edit the combined file manually).

## Testing & validation
- Run `dotnet build KnowledgeWorks_20250820_082416.sln -c Debug` to ensure the solution compiles and analyzers pass.
- Run `dotnet test KnowledgeWorks_20250820_082416.sln -c Debug` before completion; add more targeted test runs if you touch only specific projects and need quicker feedback first.
- For UI-affecting changes, smoke-test the WPF app via `dotnet run --project src/LM.App.Wpf/LM.App.Wpf.csproj -c Debug` and capture screenshots when instructed.

## Communication & commits
- Use descriptive commit messages summarizing the functional change.
- Reference any manual steps (schema migrations, data updates) in commit bodies or PR notes so future agents/users can reproduce them.
