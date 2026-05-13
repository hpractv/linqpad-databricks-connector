using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LinqPad.Databricks.Driver.Catalog;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Tests
{
    public class UnityCatalogClientTests
    {
        private const string WorkspaceUrl = "https://adb-123.azuredatabricks.net";
        private const string Pat = "test-pat";

        /// <summary>
        /// Returns responses in FIFO order; throws if the queue is empty.
        /// </summary>
        private class QueuedFakeHandler : DelegatingHandler
        {
            private readonly Queue<(HttpStatusCode status, string json)> _responses;
            public List<string> CapturedUrls { get; } = new();

            public QueuedFakeHandler(IEnumerable<(HttpStatusCode, string)> responses)
            {
                _responses = new Queue<(HttpStatusCode, string)>(responses);
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedUrls.Add(request.RequestUri?.PathAndQuery ?? "");
                if (_responses.Count == 0)
                    throw new InvalidOperationException("No more queued responses.");
                var (status, json) = _responses.Dequeue();
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json)
                });
            }
        }

        private static (DatabricksHttpClient http, QueuedFakeHandler handler) BuildClient(
            params (HttpStatusCode status, string json)[] responses)
        {
            var handler = new QueuedFakeHandler(responses);
            var http = new DatabricksHttpClient(WorkspaceUrl, Pat, handler);
            return (http, handler);
        }

        // ── ListCatalogsAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ListCatalogsAsync_SinglePage_ReturnsItems()
        {
            var json = """{"catalogs":[{"name":"cat1","comment":"first"},{"name":"cat2"}]}""";
            var (http, _) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            var results = await client.ListCatalogsAsync();

            Assert.Equal(2, results.Count);
            Assert.Equal("cat1", results[0].Name);
            Assert.Equal("first", results[0].Comment);
            Assert.Equal("cat2", results[1].Name);
        }

        [Fact]
        public async Task ListCatalogsAsync_MultiPage_CombinesResults()
        {
            var page1 = """{"catalogs":[{"name":"cat1"}],"next_page_token":"tok-abc"}""";
            var page2 = """{"catalogs":[{"name":"cat2"},{"name":"cat3"}]}""";
            var (http, handler) = BuildClient(
                (HttpStatusCode.OK, page1),
                (HttpStatusCode.OK, page2));

            var client = new UnityCatalogClient(http);
            var results = await client.ListCatalogsAsync();

            Assert.Equal(3, results.Count);
            Assert.Equal("cat1", results[0].Name);
            Assert.Equal("cat2", results[1].Name);
            Assert.Equal("cat3", results[2].Name);

            // Second request must include the page token
            Assert.Contains("page_token=tok-abc", handler.CapturedUrls[1]);
        }

        [Fact]
        public async Task ListCatalogsAsync_EmptyResponse_ReturnsEmptyList()
        {
            var json = """{"catalogs":[]}""";
            var (http, _) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            var results = await client.ListCatalogsAsync();

            Assert.Empty(results);
        }

        [Fact]
        public async Task ListCatalogsAsync_ApiError_ThrowsDatabricksApiException()
        {
            var json = """{"error":"Forbidden","error_code":"PERMISSION_DENIED"}""";
            var (http, _) = BuildClient((HttpStatusCode.Forbidden, json));

            var client = new UnityCatalogClient(http);
            var ex = await Assert.ThrowsAsync<DatabricksApiException>(
                () => client.ListCatalogsAsync());

            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("PERMISSION_DENIED", ex.ErrorCode);
        }

        // ── ListSchemasAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task ListSchemasAsync_SinglePage_ReturnsItems()
        {
            var json = """{"schemas":[{"name":"s1","catalog_name":"cat1"},{"name":"s2","catalog_name":"cat1"}]}""";
            var (http, _) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            var results = await client.ListSchemasAsync("cat1");

            Assert.Equal(2, results.Count);
            Assert.Equal("s1", results[0].Name);
            Assert.Equal("cat1", results[0].CatalogName);
        }

        [Fact]
        public async Task ListSchemasAsync_PassesCatalogNameParam()
        {
            var json = """{"schemas":[{"name":"s1","catalog_name":"my_catalog"}]}""";
            var (http, handler) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            await client.ListSchemasAsync("my_catalog");

            Assert.Contains("catalog_name=my_catalog", handler.CapturedUrls[0]);
        }

        // ── ListTablesAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task ListTablesAsync_SinglePage_ReturnsItems()
        {
            var json = """{"tables":[{"name":"t1","schema_name":"s1","catalog_name":"cat1","table_type":"MANAGED"},{"name":"v1","schema_name":"s1","catalog_name":"cat1","table_type":"VIEW"}]}""";
            var (http, _) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            var results = await client.ListTablesAsync("cat1", "s1");

            Assert.Equal(2, results.Count);
            Assert.Equal("t1", results[0].Name);
            Assert.Equal("MANAGED", results[0].TableType);
            Assert.Equal("VIEW", results[1].TableType);
        }

        [Fact]
        public async Task ListTablesAsync_PassesBothQueryParams()
        {
            var json = """{"tables":[]}""";
            var (http, handler) = BuildClient((HttpStatusCode.OK, json));

            var client = new UnityCatalogClient(http);
            await client.ListTablesAsync("my_catalog", "my_schema");

            var url = handler.CapturedUrls[0];
            Assert.Contains("catalog_name=my_catalog", url);
            Assert.Contains("schema_name=my_schema", url);
        }

        [Fact]
        public async Task ListTablesAsync_MultiPage_CombinesResults()
        {
            var page1 = """{"tables":[{"name":"t1","schema_name":"s1","catalog_name":"c1","table_type":"MANAGED"}],"next_page_token":"page2"}""";
            var page2 = """{"tables":[{"name":"t2","schema_name":"s1","catalog_name":"c1","table_type":"EXTERNAL"}]}""";
            var (http, _) = BuildClient(
                (HttpStatusCode.OK, page1),
                (HttpStatusCode.OK, page2));

            var client = new UnityCatalogClient(http);
            var results = await client.ListTablesAsync("c1", "s1");

            Assert.Equal(2, results.Count);
        }
    }
}
