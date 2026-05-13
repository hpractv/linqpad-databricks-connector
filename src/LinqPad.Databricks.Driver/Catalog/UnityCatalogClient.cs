using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Driver.Catalog
{
    // UC API response wrappers — field names differ per endpoint
    internal class CatalogsResponse
    {
        [JsonPropertyName("catalogs")]
        public List<CatalogInfo> Catalogs { get; set; } = new();

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; set; }
    }

    internal class SchemasResponse
    {
        [JsonPropertyName("schemas")]
        public List<SchemaInfo> Schemas { get; set; } = new();

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; set; }
    }

    internal class TablesResponse
    {
        [JsonPropertyName("tables")]
        public List<TableInfo> Tables { get; set; } = new();

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; set; }
    }

    public class UnityCatalogClient
    {
        private readonly DatabricksHttpClient _http;

        public UnityCatalogClient(DatabricksHttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<IReadOnlyList<CatalogInfo>> ListCatalogsAsync(CancellationToken ct = default)
        {
            var results = new List<CatalogInfo>();
            string? pageToken = null;

            do
            {
                var path = BuildPath("/api/2.1/unity-catalog/catalogs", pageToken);
                var page = await _http.GetAsync<CatalogsResponse>(path, ct).ConfigureAwait(false);
                if (page?.Catalogs is { Count: > 0 })
                    results.AddRange(page.Catalogs);
                pageToken = page?.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return results;
        }

        public async Task<IReadOnlyList<SchemaInfo>> ListSchemasAsync(string catalogName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(catalogName))
                throw new ArgumentException("catalogName is required", nameof(catalogName));

            var results = new List<SchemaInfo>();
            string? pageToken = null;

            do
            {
                var path = BuildPath($"/api/2.1/unity-catalog/schemas?catalog_name={Uri.EscapeDataString(catalogName)}", pageToken);
                var page = await _http.GetAsync<SchemasResponse>(path, ct).ConfigureAwait(false);
                if (page?.Schemas is { Count: > 0 })
                    results.AddRange(page.Schemas);
                pageToken = page?.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return results;
        }

        public async Task<IReadOnlyList<TableInfo>> ListTablesAsync(string catalogName, string schemaName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(catalogName))
                throw new ArgumentException("catalogName is required", nameof(catalogName));
            if (string.IsNullOrWhiteSpace(schemaName))
                throw new ArgumentException("schemaName is required", nameof(schemaName));

            var results = new List<TableInfo>();
            string? pageToken = null;

            do
            {
                var path = BuildPath(
                    $"/api/2.1/unity-catalog/tables?catalog_name={Uri.EscapeDataString(catalogName)}&schema_name={Uri.EscapeDataString(schemaName)}",
                    pageToken);
                var page = await _http.GetAsync<TablesResponse>(path, ct).ConfigureAwait(false);
                if (page?.Tables is { Count: > 0 })
                    results.AddRange(page.Tables);
                pageToken = page?.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return results;
        }

        public async Task<TableInfo?> GetTableAsync(string catalogName, string schemaName, string tableName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(catalogName))
                throw new ArgumentException("catalogName is required", nameof(catalogName));
            if (string.IsNullOrWhiteSpace(schemaName))
                throw new ArgumentException("schemaName is required", nameof(schemaName));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName is required", nameof(tableName));

            // UC full name format: catalog.schema.table
            var fullName = $"{Uri.EscapeDataString(catalogName)}.{Uri.EscapeDataString(schemaName)}.{Uri.EscapeDataString(tableName)}";
            return await _http.GetAsync<TableInfo>(
                $"/api/2.1/unity-catalog/tables/{fullName}", ct).ConfigureAwait(false);
        }

        private static string BuildPath(string basePath, string? pageToken)
        {
            if (string.IsNullOrEmpty(pageToken))
                return basePath;

            // basePath may already contain a '?' for query params
            var separator = basePath.Contains('?') ? '&' : '?';
            return $"{basePath}{separator}page_token={Uri.EscapeDataString(pageToken)}";
        }
    }
}
