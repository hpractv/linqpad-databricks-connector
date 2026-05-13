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

- Notes: Implemented DatabricksHttpClient, DatabricksApiException, and PagedResponse<T>. Added internal ctor to inject HttpMessageHandler for unit tests. Unit tests pass locally (4 tests).
- Tasks 1 and 2 are fully done as of 2026-05-13 planning pass. Tasks 3-9 are backlog.
- Tasks 3-9 all implemented as of 2026-05-13 implementation pass. All 21 tests pass.
- `DatabricksHttpClientTests.cs` has 2 real HTTP client tests (FakeHandler pattern). `DatabricksTests.cs` and `DriverStubTests.cs` are placeholder smoke tests.
- `PagedResponse<T>` already exists at `Models/PagedResponse.cs` — UC client (task 3) does NOT need to recreate it.
- `FakeHandler : DelegatingHandler` pattern is established in tests; use same pattern for UC and SE client tests (no Moq needed for HTTP).
- The driver csproj currently targets `net10.0` (not `net8.0-windows`) due to SDK availability on dev host. For production packaging, target `net8.0-windows` with `EnableWindowsTargeting=true`.

- The `zip` binary is not available on this dev host; `publish.sh` now uses a `python3` fallback for LPX6 creation.
- WPF event handler wiring requires DynamicMethod to create a RoutedEventHandler-compatible delegate from Action<object,object> at runtime (Delegate.CreateDelegate does not work directly from Action).
- `GetCoreFxReferenceAssemblies()` (no-arg) is obsolete; use `GetCoreFxReferenceAssemblies(cxInfo)` overload.
- `AssemblyName.CodeBase` is obsolete in .NET 10; use `Path.ChangeExtension(assemblyToBuild.Name, ".dll")` as OutputPath for CompileSource.
- ADO facade: DatabricksConnection stores StatementExecutionClient created in Open(); DatabricksCommand calls ExecuteAsync().GetAwaiter().GetResult() and wraps DataTable in CreateDataReader().
- DbParameter/DbCommand abstract overrides emit CS8765 nullability warnings; suppress with #pragma disable CS8765 on the affected setters.

- UnityCatalogClient implemented at src/LinqPad.Databricks.Driver/Catalog/UnityCatalogClient.cs; provides ListCatalogsAsync, ListSchemasAsync, and ListTablesAsync with correct pagination and query parameters. Unit tests under tests/LinqPad.Databricks.Tests/UnityCatalogClientTests.cs pass locally.
- UnityCatalogClient uses endpoint-specific response wrappers ("catalogs", "schemas", "tables") because the API does not use a generic "items" envelope; query params are URI-encoded and page_token is appended correctly.
- As of 2026-05-13 planning pass (second pass): all 8 original epic tasks are code-complete. Remaining work: (10) column details in explorer via UC GET /tables/{full_name}, (11) real ADO facade tests replacing smoke placeholders, (12) GetLastSchemaUpdate override on DatabricksDriver.
- UC column detail endpoint: GET /api/2.1/unity-catalog/tables/{catalog_name}.{schema_name}.{table_name} returns full table info including "columns" array with "name", "type_name", "comment", "nullable" fields.
- GetLastSchemaUpdate signature: `public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)` — returning null means "unknown" (LINQPad uses timeout-based refresh); returning a fixed recent time forces refresh every session.
- CORRECTED enum values (verified via MetadataLoadContext on LINQPad.Runtime.dll 1.3.1): ExplorerItemKind has: QueryableObject, Category, Schema, Parameter, Property, ReferenceLink, CollectionLink. ExplorerIcon has: Schema, Table, View, Column, Key, StoredProc, ScalarFunction, TableFunction, Parameter, ManyToOne, OneToMany, OneToOne, ManyToMany, Inherited, LinkedDatabase, Box, Blank, TableType. Previous memory entry listing these was incorrect. Use ExplorerIcon.Table/View/Column and ExplorerItemKind.Property for table/view/column items.
- As of 2026-05-13 second implementation pass: tasks 10/11/12 all done. 38 tests pass. DatabricksConnection has InjectExecutionClientForTest(StatementExecutionClient) internal method for ADO command unit tests.
- DatabricksDriver now populates column-level ExplorerItems under each table/view using UnityCatalogClient.GetTableAsync (shows "name (TYPE)"). Column fetch is best-effort and failures are skipped to keep the schema tree usable. Unit tests for GetTableAsync exist and passed in CI.
