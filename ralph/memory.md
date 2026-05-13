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
