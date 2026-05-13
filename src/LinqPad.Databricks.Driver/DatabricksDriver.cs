using System.Data;
using System.Data.Common;
using System.Reflection;
using LINQPad.Extensibility.DataContext;
using LinqPad.Databricks.Driver.Ado;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Driver;

public class DatabricksDriver : DynamicDataContextDriver
{
    public override string Name => "Azure Databricks";
    public override string Author => "Databricks LINQPad Driver";

    public override string GetConnectionDescription(IConnectionInfo cxInfo)
    {
        string url = ConnectionSettings.GetWorkspaceUrl(cxInfo);
        string warehouseId = ConnectionSettings.GetWarehouseId(cxInfo);
        return string.IsNullOrEmpty(url) ? "Databricks (not configured)"
            : string.IsNullOrEmpty(warehouseId) ? url
            : $"{url} / {warehouseId}";
    }

    public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions)
    {
        // WPF dialog is loaded at runtime via reflection (no compile-time WPF SDK dependency).
        // On non-Windows platforms this will throw; LINQPad only calls ShowConnectionDialog on Windows.
        try
        {
            return ConnectionDialog.Show(cxInfo);
        }
        catch (Exception ex)
        {
            // Re-throw with a helpful message if WPF assemblies are missing.
            throw new PlatformNotSupportedException(
                "Connection dialog requires WPF (Windows only). " + ex.Message, ex);
        }
    }

    public override List<ExplorerItem> GetSchemaAndBuildAssembly(
        IConnectionInfo cxInfo,
        AssemblyName assemblyToBuild,
        ref string nameSpace,
        ref string typeName)
    {
        string workspaceUrl = ConnectionSettings.GetWorkspaceUrl(cxInfo);
        string pat = ConnectionSettings.GetPat(cxInfo);
        string warehouseId = ConnectionSettings.GetWarehouseId(cxInfo);

        List<ExplorerItem> schema;
        try
        {
            using var http = new DatabricksHttpClient(workspaceUrl, pat);
            var ucClient = new UnityCatalogClient(http);
            schema = BuildSchema(ucClient, warehouseId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            schema =
            [
                new ExplorerItem($"Error loading schema: {ex.Message}", ExplorerItemKind.Schema, ExplorerIcon.Schema)
            ];
        }

        // Emit and compile a minimal typed data context.
        typeName = "DatabricksDataContext";
        nameSpace = "LinqPad.Databricks.Generated";
        string source = BuildContextSource(nameSpace, typeName);

        CompilationInput input = new CompilationInput
        {
            FilePathsToReference = GetCoreFxReferenceAssemblies().ToArray(),
#pragma warning disable SYSLIB0044
            OutputPath = assemblyToBuild.CodeBase,
#pragma warning restore SYSLIB0044
            SourceCode = [source],
        };

        var result = CompileSource(input);
        if (result.Errors.Length > 0)
            throw new InvalidOperationException("Compilation error: " + string.Join("\n", result.Errors));

        return schema;
    }

    public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo) => DateTime.UtcNow;

    public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
    {
        string workspaceUrl = ConnectionSettings.GetWorkspaceUrl(cxInfo);
        string pat = ConnectionSettings.GetPat(cxInfo);
        string warehouseId = ConnectionSettings.GetWarehouseId(cxInfo);

        var connection = new DatabricksConnection(workspaceUrl, pat, warehouseId);
        connection.Open();
        return connection;
    }

    public override DbProviderFactory GetProviderFactory(IConnectionInfo cxInfo) =>
        DatabricksProviderFactory.Instance;

    // ─── Schema builder ───────────────────────────────────────────────────────

    private static async Task<List<ExplorerItem>> BuildSchema(UnityCatalogClient ucClient, string warehouseId)
    {
        var catalogItems = new List<ExplorerItem>();
        IReadOnlyList<CatalogInfo> catalogs = await ucClient.ListCatalogsAsync().ConfigureAwait(false);

        foreach (CatalogInfo catalog in catalogs)
        {
            var schemaItems = new List<ExplorerItem>();
            IReadOnlyList<SchemaInfo> schemas = await ucClient.ListSchemasAsync(catalog.Name).ConfigureAwait(false);

            foreach (SchemaInfo schema in schemas)
            {
                var tableItems = new List<ExplorerItem>();
                IReadOnlyList<TableInfo> tables = await ucClient.ListTablesAsync(catalog.Name, schema.Name).ConfigureAwait(false);

                foreach (TableInfo table in tables)
                {
                    var columnItems = (table.Columns ?? [])
                        .OrderBy(c => c.Position)
                        .Select(c => new ExplorerItem($"{c.Name} ({c.TypeText})", ExplorerItemKind.Property, ExplorerIcon.Parameter)
                        {
                            SqlTypeDeclaration = c.TypeText,
                        })
                        .ToList();

                    var tableItem = new ExplorerItem(
                        table.Name,
                        ExplorerItemKind.QueryableObject,
                        table.IsView ? ExplorerIcon.TableFunction : ExplorerIcon.Blank)
                    {
                        IsEnumerable = true,
                        Children = columnItems,
                        DragText = $"{catalog.Name}.{schema.Name}.{table.Name}",
                        SqlName = $"`{catalog.Name}`.`{schema.Name}`.`{table.Name}`",
                    };
                    tableItems.Add(tableItem);
                }

                var schemaItem = new ExplorerItem(schema.Name, ExplorerItemKind.Schema, ExplorerIcon.Schema)
                {
                    Children = tableItems,
                };
                schemaItems.Add(schemaItem);
            }

            var catalogItem = new ExplorerItem(catalog.Name, ExplorerItemKind.Schema, ExplorerIcon.Schema)
            {
                Children = schemaItems,
            };
            catalogItems.Add(catalogItem);
        }

        return catalogItems;
    }

    private static string BuildContextSource(string nameSpace, string typeName) => $@"
using System;
using System.Collections.Generic;
using System.Data;

namespace {nameSpace}
{{
    /// <summary>
    /// Dynamic data context for Azure Databricks.
    /// SQL mode is enabled via the driver's GetIDbConnection override.
    /// </summary>
    public class {typeName}
    {{
        private readonly string _warehouseId;

        public {typeName}(IDbConnection connection)
        {{
            _warehouseId = (connection as global::LinqPad.Databricks.Driver.Ado.DatabricksConnection)?.WarehouseId ?? string.Empty;
        }}
    }}
}}
";
}
