using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Client;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record CatalogInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("comment")] string? Comment
);

public sealed record SchemaInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("catalog_name")] string CatalogName,
    [property: JsonPropertyName("comment")] string? Comment
);

public sealed record ColumnInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type_text")] string TypeText,
    [property: JsonPropertyName("position")] int Position
);

public sealed record TableInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema_name")] string SchemaName,
    [property: JsonPropertyName("catalog_name")] string CatalogName,
    [property: JsonPropertyName("table_type")] string TableType,
    [property: JsonPropertyName("columns")] IReadOnlyList<ColumnInfo>? Columns
)
{
    public bool IsView => string.Equals(TableType, "VIEW", StringComparison.OrdinalIgnoreCase);
}

// ─── Pagination envelope DTOs ─────────────────────────────────────────────────

internal sealed class CatalogListResponse
{
    [JsonPropertyName("catalogs")] public List<CatalogInfo>? Catalogs { get; set; }
    [JsonPropertyName("next_page_token")] public string? NextPageToken { get; set; }
}

internal sealed class SchemaListResponse
{
    [JsonPropertyName("schemas")] public List<SchemaInfo>? Schemas { get; set; }
    [JsonPropertyName("next_page_token")] public string? NextPageToken { get; set; }
}

internal sealed class TableListResponse
{
    [JsonPropertyName("tables")] public List<TableInfo>? Tables { get; set; }
    [JsonPropertyName("next_page_token")] public string? NextPageToken { get; set; }
}

// ─── Client ───────────────────────────────────────────────────────────────────

public class UnityCatalogClient
{
    private const string CatalogsBase = "api/2.1/unity-catalog/catalogs";
    private const string SchemasBase = "api/2.1/unity-catalog/schemas";
    private const string TablesBase = "api/2.1/unity-catalog/tables";

    private readonly DatabricksHttpClient _http;

    public UnityCatalogClient(DatabricksHttpClient http) => _http = http;

    public async Task<IReadOnlyList<CatalogInfo>> ListCatalogsAsync(CancellationToken ct = default)
    {
        var results = new List<CatalogInfo>();
        string? pageToken = null;
        do
        {
            string url = pageToken == null ? CatalogsBase : $"{CatalogsBase}?page_token={Uri.EscapeDataString(pageToken)}";
            CatalogListResponse page = await _http.GetAsync<CatalogListResponse>(url, ct).ConfigureAwait(false);
            if (page.Catalogs != null) results.AddRange(page.Catalogs);
            pageToken = string.IsNullOrEmpty(page.NextPageToken) ? null : page.NextPageToken;
        } while (pageToken != null);
        return results;
    }

    public async Task<IReadOnlyList<SchemaInfo>> ListSchemasAsync(string catalogName, CancellationToken ct = default)
    {
        var results = new List<SchemaInfo>();
        string? pageToken = null;
        do
        {
            string url = $"{SchemasBase}?catalog_name={Uri.EscapeDataString(catalogName)}";
            if (pageToken != null) url += $"&page_token={Uri.EscapeDataString(pageToken)}";
            SchemaListResponse page = await _http.GetAsync<SchemaListResponse>(url, ct).ConfigureAwait(false);
            if (page.Schemas != null) results.AddRange(page.Schemas);
            pageToken = string.IsNullOrEmpty(page.NextPageToken) ? null : page.NextPageToken;
        } while (pageToken != null);
        return results;
    }

    public async Task<IReadOnlyList<TableInfo>> ListTablesAsync(string catalogName, string schemaName, CancellationToken ct = default)
    {
        var results = new List<TableInfo>();
        string? pageToken = null;
        do
        {
            string url = $"{TablesBase}?catalog_name={Uri.EscapeDataString(catalogName)}&schema_name={Uri.EscapeDataString(schemaName)}";
            if (pageToken != null) url += $"&page_token={Uri.EscapeDataString(pageToken)}";
            TableListResponse page = await _http.GetAsync<TableListResponse>(url, ct).ConfigureAwait(false);
            if (page.Tables != null) results.AddRange(page.Tables);
            pageToken = string.IsNullOrEmpty(page.NextPageToken) ? null : page.NextPageToken;
        } while (pageToken != null);
        return results;
    }
}
