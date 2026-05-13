using System.Net;
using System.Text.Json;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Tests;

public class UnityCatalogClientTests
{
    // ─── ListCatalogs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListCatalogs_SinglePage_ReturnsCatalogs()
    {
        string json = """{"catalogs":[{"name":"main","comment":"Main catalog"},{"name":"hive_metastore","comment":null}]}""";
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new UnityCatalogClient(http);

        var catalogs = await client.ListCatalogsAsync();

        Assert.Equal(2, catalogs.Count);
        Assert.Equal("main", catalogs[0].Name);
        Assert.Equal("Main catalog", catalogs[0].Comment);
        Assert.Equal("hive_metastore", catalogs[1].Name);
        Assert.Null(catalogs[1].Comment);
    }

    [Fact]
    public async Task ListCatalogs_TwoPages_FollowsNextPageToken()
    {
        int callCount = 0;
        var responses = new[]
        {
            """{"catalogs":[{"name":"catalog1"}],"next_page_token":"token-page2"}""",
            """{"catalogs":[{"name":"catalog2"}]}"""
        };

        var handler = new SequentialHandler(responses.Select(r => (HttpStatusCode.OK, r)).ToArray());
        using var http = new DatabricksHttpClient("https://adb-1.azuredatabricks.net", "token", handler);
        var client = new UnityCatalogClient(http);

        var catalogs = await client.ListCatalogsAsync();

        Assert.Equal(2, catalogs.Count);
        Assert.Equal("catalog1", catalogs[0].Name);
        Assert.Equal("catalog2", catalogs[1].Name);
    }

    [Fact]
    public async Task ListCatalogs_EmptyResult_ReturnsEmptyList()
    {
        string json = """{"catalogs":[]}""";
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new UnityCatalogClient(http);

        var catalogs = await client.ListCatalogsAsync();

        Assert.Empty(catalogs);
    }

    // ─── ListSchemas ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListSchemas_ReturnsSchemas()
    {
        string json = """{"schemas":[{"name":"default","catalog_name":"main"},{"name":"silver","catalog_name":"main"}]}""";
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new UnityCatalogClient(http);

        var schemas = await client.ListSchemasAsync("main");

        Assert.Equal(2, schemas.Count);
        Assert.Equal("default", schemas[0].Name);
        Assert.Equal("main", schemas[0].CatalogName);
    }

    // ─── ListTables ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTables_DistinguishesTablesAndViews()
    {
        string json = """
            {
              "tables": [
                {"name":"orders","schema_name":"default","catalog_name":"main","table_type":"MANAGED",
                 "columns":[{"name":"id","type_text":"BIGINT","position":0},{"name":"amount","type_text":"DECIMAL","position":1}]},
                {"name":"order_summary","schema_name":"default","catalog_name":"main","table_type":"VIEW","columns":[]}
              ]
            }
            """;
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new UnityCatalogClient(http);

        var tables = await client.ListTablesAsync("main", "default");

        Assert.Equal(2, tables.Count);

        var orders = tables[0];
        Assert.Equal("orders", orders.Name);
        Assert.False(orders.IsView);
        Assert.Equal(2, orders.Columns?.Count);
        Assert.Equal("id", orders.Columns![0].Name);
        Assert.Equal(0, orders.Columns[0].Position);

        var view = tables[1];
        Assert.Equal("order_summary", view.Name);
        Assert.True(view.IsView);
    }
}
