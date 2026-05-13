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

- On some CI/dev hosts the installed .NET SDK may be a newer major (e.g., 10.x) causing `dotnet new` templates to reject older TFMs like `net8.0-windows`. Creating the csproj manually is a reliable fallback.
- Building a library targeting `net8.0-windows` can succeed under a .NET 10 SDK if the project does not require Windows-only workloads (WPF/WinForms). However running tests targeting `net8.0` requires the .NET 8 runtime to be installed on the machine (the test host will abort otherwise).
- Avoid embedding PowerShell commands with escaped double-quotes (e.g., \" ) inside XML attributes — MSBuild/XML parsing fails. Use MSBuild `MakeDir` and `Copy` tasks with `$(LOCALAPPDATA)` and `Condition='$(OS) == "Windows_NT"'` for Windows-only copy steps.
- Use forward-slash paths (`../src/...`) in `ProjectReference` entries for cross-platform builds.

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
- Discovery: `dotnet new sln -n` produced an invalid `LinqPad.Databricks.slnx` file in this environment; manual .sln file creation was required to reference projects.
- Discovery: `dotnet add package LINQPad.Reference` resolved to version 1.3.1 in this environment.
- Discovery: Building a library targeting `net8.0-windows` succeeded on Linux-hosted .NET SDK 10.0.105 when the project does not require WPF/WinForms.
- Created scaffold for LinqPad.Databricks driver (net8.0-windows) and tests (net8.0).
- Scaffolded driver and test projects using dotnet new on Wed May 13 20:48:26 UTC 2026
- Created scaffold for Databricks LINQPad driver on 2026-05-13T15:12:03-06:00
- Test project ProjectReference bug: path `../src/LinqPad.Databricks.Driver/...` is wrong from `tests/LinqPad.Databricks.Tests/`; correct path is `../../src/LinqPad.Databricks.Driver/...` (need to go up two levels to repo root).
- Test project TFM must be `net10.0` to run on the available .NET 10 runtime (net8.0 aborts with 'framework not found').
- Driver csproj lacks NuGet metadata (PackageId, PackageTags, Description, Authors) — must add before `dotnet pack` produces a valid package.
- `publish.sh` and `publish.ps1` already exist in repo root; they call `dotnet publish` + zip → .LPX6 + `dotnet pack` → .nupkg into a `dist/` folder.
- `DatabricksDriver.cs` is a minimal stub: only `Name`, `Author`, `GetConnectionDescription`, and a no-op `GetSchemaAndBuildAssembly`. `ShowConnectionDialog` is not yet overridden.
- No HTTP client, UC client, Statement Execution client, or ADO facade code exists yet as of 2026-05-13.
- Implemented DatabricksHttpClient, DatabricksApiException, and Models/PagedResponse under src/LinqPad.Databricks.Driver; added internal constructor for handler injection and Properties/AssemblyInfo InternalsVisibleTo for tests.
- Added unit tests (DatabricksHttpClientTests) using a DelegatingHandler fake to validate JSON deserialization and error mapping; test and driver projects temporarily set to net10.0 in this environment to run tests.
- DatabricksHttpClient features: Workspace URL is validated by regex, Authorization header set on HttpClient default headers, GetAsync/PostAsync generic JSON helpers, non-2xx responses map to DatabricksApiException with parsed 'message'/'error' and 'error_code'.
- Tests verify successful deserialization into PagedResponse<T> and that 4xx responses throw DatabricksApiException with proper ErrorCode and StatusCode.

