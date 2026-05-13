using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;
using LinqPad.Databricks.Driver.Catalog;
using LinqPad.Databricks.Driver.Http;
using LinqPad.Databricks.Driver.Sql;

namespace LinqPad.Databricks.Driver
{
    public class DatabricksDriver : DynamicDataContextDriver
    {
        public override string Name => "Databricks LINQPad Driver";
        public override string Author => "hpractv";

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            try
            {
                var data = cxInfo?.DriverData;
                var wsUrl = data?.Element("WorkspaceUrl")?.Value;
                var whId = data?.Element("WarehouseId")?.Value;
                if (!string.IsNullOrEmpty(wsUrl) && !string.IsNullOrEmpty(whId))
                    return $"{wsUrl} / {whId}";
                if (!string.IsNullOrEmpty(wsUrl))
                    return wsUrl;
            }
            catch { }
            return "Databricks Workspace";
        }

        // ── Connection Dialog ──────────────────────────────────────────────────

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions)
        {
            try
            {
                return ShowConnectionDialogWpf(cxInfo);
            }
            catch (Exception ex)
            {
                // WPF not available (non-Windows) — fall back to simple console-style defaults
                System.Diagnostics.Debug.WriteLine($"ShowConnectionDialog WPF failed: {ex.Message}");
                return false;
            }
        }

        private static bool ShowConnectionDialogWpf(IConnectionInfo cxInfo)
        {
            // Load WPF types via reflection so the assembly has no compile-time WPF SDK dependency
            var wpfAssembly = System.Reflection.Assembly.Load("PresentationFramework");
            var xamlReaderType = System.Type.GetType(
                "System.Windows.Markup.XamlReader, PresentationFramework", throwOnError: true)!;
            var windowType = System.Type.GetType(
                "System.Windows.Window, PresentationFramework", throwOnError: true)!;
            var logicalTreeType = System.Type.GetType(
                "System.Windows.LogicalTreeHelper, PresentationFramework", throwOnError: true)!;

            var xaml = BuildDialogXaml();
            var window = xamlReaderType.GetMethod("Parse", new[] { typeof(string) })!
                .Invoke(null, new object[] { xaml })!;

            // Read existing values from DriverData
            var data = cxInfo.DriverData;
            var wsUrl = data.Element("WorkspaceUrl")?.Value ?? "";
            var whId = data.Element("WarehouseId")?.Value ?? "";
            var defCatalog = data.Element("DefaultCatalog")?.Value ?? "";
            var defSchema = data.Element("DefaultSchema")?.Value ?? "";
            var encPat = data.Element("EncryptedPat")?.Value ?? "";
            var pat = string.IsNullOrEmpty(encPat) ? "" : cxInfo.Decrypt(encPat);

            var findNodeMethods = logicalTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            MethodInfo? findNode = null;
            foreach (var m in findNodeMethods)
            {
                var pms = m.GetParameters();
                if (m.Name == "FindLogicalNode" && pms.Length == 2 && pms[1].ParameterType == typeof(string))
                {
                    findNode = m;
                    break;
                }
            }

            object? GetControl(string name) =>
                findNode?.Invoke(null, new[] { window, (object)name });

            void SetText(string controlName, string value)
            {
                var ctrl = GetControl(controlName);
                if (ctrl is null) return;
                var prop = ctrl.GetType().GetProperty("Text") ?? ctrl.GetType().GetProperty("Password");
                prop?.SetValue(ctrl, value);
            }

            string GetText(string controlName)
            {
                var ctrl = GetControl(controlName);
                if (ctrl is null) return "";
                var prop = ctrl.GetType().GetProperty("Password") ?? ctrl.GetType().GetProperty("Text");
                return prop?.GetValue(ctrl) as string ?? "";
            }

            SetText("WorkspaceUrl", wsUrl);
            SetText("WarehouseId", whId);
            SetText("PersonalAccessToken", pat);
            SetText("DefaultCatalog", defCatalog);
            SetText("DefaultSchema", defSchema);

            // Wire OK button to set DialogResult = true
            var okBtn = GetControl("OkButton");
            if (okBtn is not null)
            {
                var okClickEvent = okBtn.GetType().GetEvent("Click");
                if (okClickEvent is not null)
                {
                    var okHandlerType = okClickEvent.EventHandlerType!;
                    Action<object, object> okHandler = (s, e) =>
                        windowType.GetProperty("DialogResult")?.SetValue(window, (bool?)true);
                    var okDelegate = CreateCompatibleDelegate(okHandlerType, okHandler);
                    okClickEvent.AddEventHandler(okBtn, okDelegate);
                }
            }

            // Wire Test Connection button via reflection (avoid compile-time RoutedEventHandler ref)
            var testBtn = GetControl("TestConnectionButton");
            if (testBtn is not null)
            {
                var clickEvent = testBtn.GetType().GetEvent("Click");
                var statusLabel = GetControl("StatusLabel");
                if (clickEvent is not null)
                {
                    var handlerType = clickEvent.EventHandlerType!;
                    Action<object, object> handler = (s, e) =>
                    {
                        var url = GetText("WorkspaceUrl");
                        var token = GetText("PersonalAccessToken");
                        try
                        {
                            using var http = new DatabricksHttpClient(url, token);
                            var ucClient = new UnityCatalogClient(http);
                            var cats = ucClient.ListCatalogsAsync().GetAwaiter().GetResult();
                            SetStatus(statusLabel, $"Connected — {cats.Count} catalog(s) found.", success: true);
                        }
                        catch (Exception ex)
                        {
                            SetStatus(statusLabel, $"Failed: {ex.Message}", success: false);
                        }
                    };
                    clickEvent.AddEventHandler(testBtn, CreateCompatibleDelegate(handlerType, handler));
                }
            }

            // Show dialog
            bool? result = windowType
                .GetMethod("ShowDialog", Type.EmptyTypes)?
                .Invoke(window, null) as bool?;

            if (result == true)
            {
                var newUrl = GetText("WorkspaceUrl").TrimEnd('/');
                var newToken = GetText("PersonalAccessToken");
                var newWhId = GetText("WarehouseId");
                var newCatalog = GetText("DefaultCatalog");
                var newSchema = GetText("DefaultSchema");

                data.SetElementValue("WorkspaceUrl", newUrl);
                data.SetElementValue("WarehouseId", newWhId);
                data.SetElementValue("DefaultCatalog", newCatalog);
                data.SetElementValue("DefaultSchema", newSchema);
                data.SetElementValue("EncryptedPat",
                    string.IsNullOrEmpty(newToken) ? "" : cxInfo.Encrypt(newToken));
                return true;
            }
            return false;
        }

        private static void SetStatus(object? label, string text, bool success)
        {
            if (label is null) return;
            var textProp = label.GetType().GetProperty("Content") ?? label.GetType().GetProperty("Text");
            textProp?.SetValue(label, text);
            var fgProp = label.GetType().GetProperty("Foreground");
            if (fgProp is not null)
            {
                var brushType = System.Type.GetType(
                    "System.Windows.Media.Brushes, PresentationCore", throwOnError: false);
                if (brushType is not null)
                {
                    var brush = brushType.GetProperty(success ? "Green" : "Red")?.GetValue(null);
                    if (brush is not null) fgProp.SetValue(label, brush);
                }
            }
        }

        private static Delegate CreateCompatibleDelegate(Type delegateType, Action<object, object> handler)
        {
            // RoutedEventHandler has signature (object sender, RoutedEventArgs e)
            // We wrap our Action<object,object> in a dynamic method compatible with that signature
            var invokeMethod = delegateType.GetMethod("Invoke")!;
            var paramTypes = invokeMethod.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            var dm = new System.Reflection.Emit.DynamicMethod(
                "ClickBridge", typeof(void), new[] { typeof(Action<object, object>) }.Concat(paramTypes).ToArray(),
                typeof(DatabricksDriver).Module);

            var il = dm.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);  // handler (Action<object,object>)
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);  // sender
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_2);  // event args
            il.Emit(System.Reflection.Emit.OpCodes.Callvirt,
                typeof(Action<object, object>).GetMethod("Invoke")!);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return dm.CreateDelegate(delegateType, handler);
        }

        private static string BuildDialogXaml() => """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Databricks Connection" Height="380" Width="460"
                    WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
              <Window.Resources>
                <Style TargetType="Label"><Setter Property="Margin" Value="0,4,4,0"/></Style>
                <Style TargetType="TextBox"><Setter Property="Margin" Value="0,4,0,0"/><Setter Property="Padding" Value="4,2"/></Style>
                <Style TargetType="PasswordBox"><Setter Property="Margin" Value="0,4,0,0"/><Setter Property="Padding" Value="4,2"/></Style>
                <Style TargetType="Button"><Setter Property="Margin" Value="4,4,0,0"/><Setter Property="Padding" Value="12,4"/></Style>
              </Window.Resources>
              <Grid Margin="16">
                <Grid.RowDefinitions>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="*"/>
                  <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="140"/>
                  <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <Label Grid.Row="0" Grid.Column="0">Workspace URL</Label>
                <TextBox Grid.Row="0" Grid.Column="1" x:Name="WorkspaceUrl"
                         ToolTip="e.g. https://adb-1234567890.1.azuredatabricks.net"/>

                <Label Grid.Row="1" Grid.Column="0">Personal Access Token</Label>
                <PasswordBox Grid.Row="1" Grid.Column="1" x:Name="PersonalAccessToken"/>

                <Label Grid.Row="2" Grid.Column="0">Warehouse ID</Label>
                <TextBox Grid.Row="2" Grid.Column="1" x:Name="WarehouseId"/>

                <Label Grid.Row="3" Grid.Column="0">Default Catalog</Label>
                <TextBox Grid.Row="3" Grid.Column="1" x:Name="DefaultCatalog" ToolTip="Optional"/>

                <Label Grid.Row="4" Grid.Column="0">Default Schema</Label>
                <TextBox Grid.Row="4" Grid.Column="1" x:Name="DefaultSchema" ToolTip="Optional"/>

                <Button Grid.Row="5" Grid.Column="1" x:Name="TestConnectionButton"
                        HorizontalAlignment="Left">Test Connection</Button>

                <Label Grid.Row="6" Grid.Column="0" Grid.ColumnSpan="2"
                       x:Name="StatusLabel" Foreground="Gray" Content=""/>

                <StackPanel Grid.Row="8" Grid.Column="0" Grid.ColumnSpan="2"
                            Orientation="Horizontal" HorizontalAlignment="Right">
                  <Button x:Name="OkButton" IsDefault="True">OK</Button>
                  <Button IsCancel="True">Cancel</Button>
                </StackPanel>
              </Grid>
            </Window>
            """;

        // ── Schema Explorer + Context Assembly ────────────────────────────────

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            nameSpace = "LinqPad.Databricks";
            typeName = "DatabricksContext";

            var items = new List<ExplorerItem>();

            try
            {
                var (http, uc) = BuildClients(cxInfo);
                using (http)
                {
                    var catalogs = uc.ListCatalogsAsync().GetAwaiter().GetResult();

                    var defCatalog = cxInfo.DriverData.Element("DefaultCatalog")?.Value ?? "";
                    var defSchema = cxInfo.DriverData.Element("DefaultSchema")?.Value ?? "";

                    // Sort default catalog first
                    var orderedCatalogs = catalogs
                        .OrderByDescending(c => string.Equals(c.Name, defCatalog, StringComparison.OrdinalIgnoreCase))
                        .ThenBy(c => c.Name);

                    foreach (var cat in orderedCatalogs)
                    {
                        var catItem = new ExplorerItem(cat.Name, ExplorerItemKind.Schema, ExplorerIcon.Schema)
                        {
                            IsEnumerable = false,
                            Children = new List<ExplorerItem>()
                        };

                        try
                        {
                            var schemas = uc.ListSchemasAsync(cat.Name).GetAwaiter().GetResult();
                            var orderedSchemas = schemas
                                .OrderByDescending(s => string.Equals(s.Name, defSchema, StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(cat.Name, defCatalog, StringComparison.OrdinalIgnoreCase))
                                .ThenBy(s => s.Name);

                            foreach (var schema in orderedSchemas)
                            {
                                var schemaItem = new ExplorerItem(schema.Name, ExplorerItemKind.Schema, ExplorerIcon.Schema)
                                {
                                    IsEnumerable = false,
                                    Children = new List<ExplorerItem>()
                                };

                                try
                                {
                                    var tables = uc.ListTablesAsync(cat.Name, schema.Name).GetAwaiter().GetResult();
                                    foreach (var tbl in tables.OrderBy(t => t.Name))
                                    {
                                        bool isView = tbl.TableType is "VIEW" or "MATERIALIZED_VIEW";
                                        var icon = isView ? ExplorerIcon.View : ExplorerIcon.Table;
                                        var tblItem = new ExplorerItem(tbl.Name, ExplorerItemKind.QueryableObject, icon)
                                        {
                                            IsEnumerable = true,
                                            DragText = $"`{cat.Name}`.`{schema.Name}`.`{tbl.Name}`",
                                            Children = new List<ExplorerItem>()
                                        };

                                        // Fetch column details for this table
                                        try
                                        {
                                            var detail = uc.GetTableAsync(cat.Name, schema.Name, tbl.Name).GetAwaiter().GetResult();
                                            if (detail?.Columns is { Count: > 0 })
                                            {
                                                foreach (var col in detail.Columns.OrderBy(c => c.Position))
                                                {
                                                    var colLabel = string.IsNullOrEmpty(col.TypeName)
                                                        ? col.Name
                                                        : $"{col.Name} ({col.TypeName})";
                                                    tblItem.Children.Add(new ExplorerItem(
                                                        colLabel,
                                                        ExplorerItemKind.Property,
                                                        ExplorerIcon.Blank)
                                                    {
                                                        ToolTipText = col.Comment
                                                    });
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Column detail fetch is best-effort; skip silently to keep tree usable
                                        }

                                        schemaItem.Children.Add(tblItem);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    schemaItem.Children.Add(ErrorItem($"Error loading tables: {ex.Message}"));
                                }

                                catItem.Children.Add(schemaItem);
                            }
                        }
                        catch (Exception ex)
                        {
                            catItem.Children.Add(ErrorItem($"Error loading schemas: {ex.Message}"));
                        }

                        items.Add(catItem);
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(ErrorItem($"Connection error: {ex.Message}"));
            }

            // Emit a minimal context assembly
            var src = BuildContextSource(nameSpace, typeName);
            var refs = GetCoreFxReferenceAssemblies(cxInfo)
                .Append(typeof(DatabricksDriver).Assembly.Location)
                .ToArray();

            try
            {
                var result = CompileSource(new CompilationInput
                {
                    SourceCode = new[] { src },
                    FilePathsToReference = refs,
                    OutputPath = System.IO.Path.ChangeExtension(assemblyToBuild.Name, ".dll")
                });
                if (result.Errors.Length > 0)
                    items.Insert(0, ErrorItem($"Context compile error: {result.Errors[0]}"));
            }
            catch (Exception ex)
            {
                items.Insert(0, ErrorItem($"Context compile failed: {ex.Message}"));
            }

            return items;
        }

        private static (DatabricksHttpClient http, UnityCatalogClient uc) BuildClients(IConnectionInfo cxInfo)
        {
            var data = cxInfo.DriverData;
            var url = data.Element("WorkspaceUrl")?.Value ?? "";
            var encPat = data.Element("EncryptedPat")?.Value ?? "";
            var pat = string.IsNullOrEmpty(encPat) ? "" : cxInfo.Decrypt(encPat);
            var http = new DatabricksHttpClient(url, pat);
            return (http, new UnityCatalogClient(http));
        }

        private static ExplorerItem ErrorItem(string message) =>
            new ExplorerItem(message, ExplorerItemKind.Parameter, ExplorerIcon.Blank);

        private static string BuildContextSource(string ns, string type) => $@"
using System;
using System.Data;
using LinqPad.Databricks.Driver.Sql;
using LinqPad.Databricks.Driver.Http;

namespace {ns}
{{
    public class {type}
    {{
        private readonly StatementExecutionClient _exec;
        private readonly string _warehouseId;

        public {type}(StatementExecutionClient exec, string warehouseId)
        {{
            _exec = exec;
            _warehouseId = warehouseId;
        }}

        public DataTable Query(string sql) =>
            _exec.ExecuteAsync(_warehouseId, sql).GetAwaiter().GetResult();
    }}
}}
";

        // ── ADO / SQL Mode ─────────────────────────────────────────────────────

        public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
        {
            var data = cxInfo.DriverData;
            var url = data.Element("WorkspaceUrl")?.Value ?? "";
            var encPat = data.Element("EncryptedPat")?.Value ?? "";
            var pat = string.IsNullOrEmpty(encPat) ? "" : cxInfo.Decrypt(encPat);
            var whId = data.Element("WarehouseId")?.Value ?? "";

            var conn = new Ado.DatabricksConnection(url, pat, whId);
            conn.Open();
            return conn;
        }

        // Return null so LINQPad uses its own timeout-based cache strategy.
        // Databricks REST APIs do not expose a catalog-level DDL timestamp,
        // so we cannot cheaply detect schema changes between sessions.
        public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo) => null;
    }
}

