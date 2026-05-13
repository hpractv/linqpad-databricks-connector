using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;

namespace LinqPad.Databricks.Driver;

/// <summary>
/// Helpers for reading and writing typed fields to/from <see cref="IConnectionInfo.DriverData"/>.
/// All secrets are stored encrypted via IConnectionInfo.Encrypt/Decrypt.
/// </summary>
internal static class ConnectionSettings
{
    public static string GetWorkspaceUrl(IConnectionInfo cxInfo) =>
        cxInfo.DriverData.Element("WorkspaceUrl")?.Value ?? string.Empty;

    public static void SetWorkspaceUrl(IConnectionInfo cxInfo, string value) =>
        SetElement(cxInfo, "WorkspaceUrl", value);

    public static string GetPat(IConnectionInfo cxInfo)
    {
        string? encrypted = cxInfo.DriverData.Element("Pat")?.Value;
        return string.IsNullOrEmpty(encrypted) ? string.Empty : cxInfo.Decrypt(encrypted);
    }

    public static void SetPat(IConnectionInfo cxInfo, string plainText) =>
        SetElement(cxInfo, "Pat", cxInfo.Encrypt(plainText));

    public static string GetWarehouseId(IConnectionInfo cxInfo) =>
        cxInfo.DriverData.Element("WarehouseId")?.Value ?? string.Empty;

    public static void SetWarehouseId(IConnectionInfo cxInfo, string value) =>
        SetElement(cxInfo, "WarehouseId", value);

    public static string GetDefaultCatalog(IConnectionInfo cxInfo) =>
        cxInfo.DriverData.Element("DefaultCatalog")?.Value ?? string.Empty;

    public static void SetDefaultCatalog(IConnectionInfo cxInfo, string value) =>
        SetElement(cxInfo, "DefaultCatalog", value);

    public static string GetDefaultSchema(IConnectionInfo cxInfo) =>
        cxInfo.DriverData.Element("DefaultSchema")?.Value ?? string.Empty;

    public static void SetDefaultSchema(IConnectionInfo cxInfo, string value) =>
        SetElement(cxInfo, "DefaultSchema", value);

    private static void SetElement(IConnectionInfo cxInfo, string name, string value)
    {
        XElement? el = cxInfo.DriverData.Element(name);
        if (el == null)
        {
            el = new XElement(name);
            cxInfo.DriverData.Add(el);
        }
        el.Value = value;
    }
}
