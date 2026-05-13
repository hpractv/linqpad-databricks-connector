# Project Memory

This file is maintained by the Ralph loop. Each plan, dev, and QA phase reads it for context and appends new discoveries.

Keep entries concise and non-obvious. Remove entries that are no longer relevant.

## Commands

<!-- Build, test, and lint commands discovered during the loop. Example:
- Build: `npm run build`
- Test: `npm run test:ci`
- Lint: `npm run lint`
-->

## Conventions

<!-- Code patterns, naming conventions, and project standards. Example:
- Use named exports (no default exports)
- Tests live alongside source files as *.test.ts
-->

## Gotchas

<!-- Non-obvious issues, environment quirks, or things that caused failures. Example:
- Windows paths require backslashes in spawn() args
- npm install must run before tsx can resolve modules
-->
## Project State

- As of initial plan pass, the repo is empty (no src/ or tests/ directories, no .csproj/.sln files).
- All 8 epic tasks are pending from scratch.
- Target TFM: `net8.0-windows` for Windows-first delivery; note NuGet lib folder should use `net8.0` (no `-windows`) for macOS compatibility per doc.
- LINQPad NuGet ref: `LINQPad.Reference` package; base class is `DynamicDataContextDriver` in `LINQPad.Extensibility.DataContext`.
- Auth: PAT via `Authorization: Bearer <token>`; store with `IConnectionInfo.Encrypt/Decrypt`.
- Unity Catalog base path: `/api/2.1/unity-catalog/` ; Statement Execution: `/api/2.0/sql/statements`.
- Connection dialog must use WPF (hosted under XPF on macOS); no WPF references outside `ShowConnectionDialog`.
- Post-build event should copy output to `%localappdata%\LINQPad\Drivers\DataContext\NetCore\<DriverName>` for dev loop.
- Packaging: zip output → rename `.LPX6`; NuGet package ID must match driver assembly name; tag `linqpaddriver`.

## Build Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Package: `./publish.sh [Release] [version]` or `./publish.ps1`

## LINQPad API Discoveries

- `ExplorerItemKind` values: `Category`, `CollectionLink`, `FieldOrProperty`, `Parameter`, `QueryableObject`, `ScalarFunction`, `Schema`, `StoredProc`
  - No `Column`, `Table`, or `View` values exist in this enum.
- `ExplorerIcon` values: `Blank`, `LinkedDatabase`, `ManyToMany`, `ManyToOne`, `OneToMany`, `ScalarFunction`, `Schema`, `StoredProc`, `TableFunction`, `Box`, `Parameter`
  - No `Table`, `View`, or `Column` values. Use `Blank` for tables, `TableFunction` for views.
- `CompilationInput` properties: `FilePathsToReference` (string[]), `OutputPath` (string), `SourceCode` (string[])
- `GetCoreFxReferenceAssemblies()` on `DataContextDriver` returns the .NET Core framework reference assemblies for use in `CompilationInput.FilePathsToReference`.
- `LINQPad.Reference` v1.3.1 is a reference-only assembly (no runtime); only works with `MetadataLoadContext`.

## Project Conventions

- TFM: `net10.0` (matches installed SDK); for Windows deployment, production should use `net8.0-windows` with `EnableWindowsTargeting=true` (on Windows with WPF SDK).
- WPF dialog: implemented via reflection (`System.Windows.Markup.XamlReader.Parse` + `LogicalTreeHelper.FindLogicalNode`) to avoid compile-time WPF SDK dependency. Parses XAML at runtime.
- `InternalsVisibleTo("LinqPad.Databricks.Tests")` in `AssemblyInfo.cs` exposes internal 3-arg `DatabricksHttpClient` constructor to tests.
- Moq: use `MockBehavior.Loose` (default), not `Strict`, to avoid issues with `Dispose(bool)` calls on `HttpMessageHandler` mock.
- URL validation regex: `^https://[a-zA-Z0-9\-\.]+(\.azuredatabricks\.net|\.databricks\.azure\.cn)/?$` - must allow dots in subdomain (e.g., `adb-1234.1.azuredatabricks.net`).
- Created solution and projects for Databricks LINQPad driver manually (csproj files written directly).
