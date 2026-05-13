// ConnectionDialog - WPF connection dialog loaded via XAML at runtime.
// WPF types are loaded via reflection to avoid compile-time WPF SDK dependency
// (not available in cross-platform CI builds). LINQPad provides WPF assemblies
// in the driver process on Windows.

using System.Reflection;
using LINQPad.Extensibility.DataContext;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Driver;

internal static class ConnectionDialog
{
    private const string XamlSource = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='Azure Databricks Connection' Height='330' Width='520'
        ResizeMode='NoResize' WindowStartupLocation='CenterOwner' ShowInTaskbar='False'>
    <Grid Margin='15'>
        <Grid.RowDefinitions>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='*'/>
            <RowDefinition Height='Auto'/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width='140'/>
            <ColumnDefinition Width='*'/>
        </Grid.ColumnDefinitions>
        <Label Grid.Row='0' Grid.Column='0' VerticalAlignment='Center'>Workspace URL *</Label>
        <TextBox Grid.Row='0' Grid.Column='1' Name='WorkspaceUrlBox' Margin='0,4'/>
        <Label Grid.Row='1' Grid.Column='0' VerticalAlignment='Center'>Personal Access Token *</Label>
        <PasswordBox Grid.Row='1' Grid.Column='1' Name='PatBox' Margin='0,4'/>
        <Label Grid.Row='2' Grid.Column='0' VerticalAlignment='Center'>SQL Warehouse ID *</Label>
        <TextBox Grid.Row='2' Grid.Column='1' Name='WarehouseIdBox' Margin='0,4'/>
        <Label Grid.Row='3' Grid.Column='0' VerticalAlignment='Center'>Default Catalog</Label>
        <TextBox Grid.Row='3' Grid.Column='1' Name='DefaultCatalogBox' Margin='0,4'/>
        <Label Grid.Row='4' Grid.Column='0' VerticalAlignment='Center'>Default Schema</Label>
        <TextBox Grid.Row='4' Grid.Column='1' Name='DefaultSchemaBox' Margin='0,4'/>
        <Button Grid.Row='5' Grid.Column='1' Name='TestBtn' HorizontalAlignment='Left'
                Margin='0,8' Padding='8,4'>Test Connection</Button>
        <StackPanel Grid.Row='7' Grid.Column='0' Grid.ColumnSpan='2'
                    Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,8,0,0'>
            <Button Name='OkBtn' IsDefault='True' Margin='0,0,8,0' Padding='20,5'>OK</Button>
            <Button Name='CancelBtn' IsCancel='True' Padding='20,5'>Cancel</Button>
        </StackPanel>
    </Grid>
</Window>";

    public static bool Show(IConnectionInfo cxInfo)
    {
        Assembly pf = Assembly.Load("PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

        Type xamlReader = pf.GetType("System.Windows.Markup.XamlReader")!;
        object window = xamlReader.InvokeMember("Parse",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
            null, null, new object[] { XamlSource })!;
        Type windowType = window.GetType();

        object? FindName(string name)
        {
            Type lth = pf.GetType("System.Windows.LogicalTreeHelper")!;
            return lth.InvokeMember("FindLogicalNode",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                null, null, new object[] { window, name });
        }

        object? urlBox = FindName("WorkspaceUrlBox");
        object? patBox = FindName("PatBox");
        object? warehouseBox = FindName("WarehouseIdBox");
        object? catalogBox = FindName("DefaultCatalogBox");
        object? schemaBox = FindName("DefaultSchemaBox");
        object? okBtn = FindName("OkBtn");
        object? cancelBtn = FindName("CancelBtn");
        object? testBtn = FindName("TestBtn");

        SetText(urlBox, ConnectionSettings.GetWorkspaceUrl(cxInfo));
        SetPassword(patBox, ConnectionSettings.GetPat(cxInfo));
        SetText(warehouseBox, ConnectionSettings.GetWarehouseId(cxInfo));
        SetText(catalogBox, ConnectionSettings.GetDefaultCatalog(cxInfo));
        SetText(schemaBox, ConnectionSettings.GetDefaultSchema(cxInfo));

        bool result = false;

        AddClick(okBtn, () =>
        {
            string url = GetText(urlBox).Trim();
            string pat = GetPassword(patBox);
            string warehouse = GetText(warehouseBox).Trim();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(warehouse))
            {
                ShowMsg(pf, "Workspace URL, PAT, and Warehouse ID are required.");
                return;
            }
            ConnectionSettings.SetWorkspaceUrl(cxInfo, url);
            ConnectionSettings.SetPat(cxInfo, pat);
            ConnectionSettings.SetWarehouseId(cxInfo, warehouse);
            ConnectionSettings.SetDefaultCatalog(cxInfo, GetText(catalogBox).Trim());
            ConnectionSettings.SetDefaultSchema(cxInfo, GetText(schemaBox).Trim());
            result = true;
            windowType.GetProperty("DialogResult")?.SetValue(window, (bool?)true);
        });

        AddClick(cancelBtn, () => windowType.GetProperty("DialogResult")?.SetValue(window, (bool?)false));

        AddClick(testBtn, () =>
        {
            Task.Run(async () =>
            {
                string url = GetText(urlBox).Trim();
                string pat = GetPassword(patBox);
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(pat))
                {
                    ShowMsg(pf, "Enter Workspace URL and PAT first.");
                    return;
                }
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var http = new DatabricksHttpClient(url, pat);
                    var catalogs = await new UnityCatalogClient(http).ListCatalogsAsync(cts.Token);
                    string first = catalogs.Count > 0 ? catalogs[0].Name : "(no catalogs)";
                    ShowMsg(pf, $"Connection succeeded. First catalog: {first}");
                }
                catch (Exception ex)
                {
                    ShowMsg(pf, $"Connection failed: {ex.Message}");
                }
            }).GetAwaiter().GetResult();
        });

        windowType.InvokeMember("ShowDialog",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
            null, window, null);

        return result;
    }

    private static string GetText(object? c) =>
        (string?)c?.GetType().GetProperty("Text")?.GetValue(c) ?? string.Empty;
    private static string GetPassword(object? c) =>
        (string?)c?.GetType().GetProperty("Password")?.GetValue(c) ?? string.Empty;
    private static void SetText(object? c, string v) =>
        c?.GetType().GetProperty("Text")?.SetValue(c, v);
    private static void SetPassword(object? c, string v) =>
        c?.GetType().GetProperty("Password")?.SetValue(c, v);
    private static void ShowMsg(Assembly pf, string msg) =>
        pf.GetType("System.Windows.MessageBox")!.InvokeMember("Show",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
            null, null, new object[] { msg });

    private static void AddClick(object? btn, Action action)
    {
        if (btn == null) return;
        EventInfo? ev = btn.GetType().GetEvent("Click");
        if (ev == null) return;
        // Create a delegate matching RoutedEventHandler(object, RoutedEventArgs)
        var handler = new EventHandler((_, _) => action());
        Type? handlerType = ev.EventHandlerType;
        if (handlerType == null) return;
        Delegate d = Delegate.CreateDelegate(handlerType, handler, typeof(EventHandler).GetMethod("Invoke")!);
        ev.AddEventHandler(btn, d);
    }
}
