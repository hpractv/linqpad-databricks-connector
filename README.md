# Azure Databricks LINQPad Driver

A [LINQPad 8+](https://www.linqpad.net/) Data Context Driver for Azure Databricks.

Connects to Azure Databricks using:
- **Unity Catalog REST API** (`/api/2.1/unity-catalog/`) for schema discovery (catalogs → schemas → tables/views)
- **SQL Statement Execution API** (`/api/2.0/sql/statements`) for running SQL queries against a SQL Warehouse

## Features

- Browse Unity Catalog hierarchy in LINQPad's Schema Explorer
- Run Databricks SQL dialect queries directly in LINQPad (SQL mode)
- Encrypted PAT storage via LINQPad's built-in credential encryption
- Paginated catalog/schema/table enumeration (handles large workspaces)
- Multi-chunk result set handling for large query results

## Prerequisites

- LINQPad 8 or later
- An Azure Databricks workspace with Unity Catalog enabled
- A SQL Warehouse (HTTP path / warehouse ID)
- A Personal Access Token (PAT) with `USE CATALOG` / `USE SCHEMA` permissions

## Installation

### Option A — `.LPX6` file (file-based install)

1. Build the package (see [Building](#building) below) or download a release `.LPX6`.
2. In LINQPad → **Add connection** → **View more drivers** → **Browse** → select the `.LPX6` file.

### Option B — NuGet (recommended for teams)

1. In LINQPad → **Add connection** → **View more drivers** → **Find NuGet packages** → search `linqpaddriver` or `LinqPad.Databricks`.
2. Select and install **LinqPad.Databricks.Driver**.

*(For a private NuGet feed, add the feed URL in LINQPad's NuGet settings first.)*

## Connection Settings

| Field | Required | Description |
|-------|----------|-------------|
| Workspace URL | ✅ | `https://adb-<id>.<region>.azuredatabricks.net` |
| Personal Access Token | ✅ | Databricks PAT (stored encrypted) |
| SQL Warehouse ID | ✅ | Warehouse GUID from Databricks SQL Warehouse settings |
| Default Catalog | ➖ | Optional default catalog |
| Default Schema | ➖ | Optional default schema |

## Using the Driver

1. **Add connection**: choose **Azure Databricks** from the driver list and fill in the dialog.
2. **Schema Explorer**: expand the connection to browse catalogs → schemas → tables/views with columns.
3. **SQL mode**: set query language to **SQL** and run Databricks SQL directly.

## Building

```bash
# Run all unit tests
dotnet test

# Produce .LPX6 and .nupkg artifacts in ./dist/
./publish.sh          # Linux/macOS
./publish.ps1         # Windows PowerShell
```

### Development loop (Windows)

After building in Debug configuration, the driver is automatically copied to:
```
%LOCALAPPDATA%\LINQPad\Drivers\DataContext\NetCore\DatabricksDriver\
```
Restart LINQPad (or use **Reload driver**) to pick up changes.

## Architecture

```
DatabricksDriver (DynamicDataContextDriver)
├── ConnectionSettings         — IConnectionInfo DriverData helpers
├── ConnectionDialog           — WPF dialog via reflection (runtime WPF, no compile-time dep)
├── Client/
│   ├── DatabricksHttpClient   — HTTP base client, PAT auth, JSON, error mapping
│   ├── UnityCatalogClient     — /api/2.1/unity-catalog/* with pagination
│   └── StatementExecutionClient — /api/2.0/sql/statements with polling & chunk fetch
└── Ado/
    ├── DatabricksConnection   — DbConnection wrapping the REST client
    ├── DatabricksCommand      — DbCommand → StatementExecutionClient
    ├── DatabricksDataReader   — DbDataReader over StatementResult
    └── DatabricksProviderFactory — DbProviderFactory
```

## Limitations (v0.1.0)

- **SQL mode only** for query execution (no LINQ translation).
- **PAT authentication only** (Azure AD / OAuth not yet implemented).
- **Windows-first** for the connection dialog (WPF loaded at runtime; macOS works via LINQPad's XPF host).
- Large result sets use inline JSON; `EXTERNAL_LINKS` disposition for very large results is not yet supported.

## License

See [LICENSE](LICENSE) for details.
