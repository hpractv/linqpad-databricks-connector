using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LinqPad.Databricks.Driver.Http;
using LinqPad.Databricks.Driver.Sql;

namespace LinqPad.Databricks.Tests
{
    public class StatementExecutionClientTests
    {
        private const string WorkspaceUrl = "https://adb-123.azuredatabricks.net";
        private const string Pat = "test-pat";
        private const string WarehouseId = "wh-abc123";

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

        private static (StatementExecutionClient client, QueuedFakeHandler handler) Build(
            params (HttpStatusCode status, string json)[] responses)
        {
            var handler = new QueuedFakeHandler(responses);
            var http = new DatabricksHttpClient(WorkspaceUrl, Pat, handler);
            return (new StatementExecutionClient(http), handler);
        }

        private static string SucceededResponse(string statementId = "stmt-1") => $$"""
            {
              "statement_id": "{{statementId}}",
              "status": { "state": "SUCCEEDED" },
              "result": {
                "schema": { "columns": [{"name":"id","type_name":"LONG"},{"name":"val","type_name":"STRING"}] },
                "data_array": [["1","hello"],["2","world"]]
              }
            }
            """;

        // ── Immediate SUCCEEDED ────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_ImmediateSucceeded_ReturnsDataTable()
        {
            var (client, _) = Build((HttpStatusCode.OK, SucceededResponse()));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT 1");

            Assert.Equal(2, dt.Columns.Count);
            Assert.Equal("id", dt.Columns[0].ColumnName);
            Assert.Equal("val", dt.Columns[1].ColumnName);
            Assert.Equal(2, dt.Rows.Count);
            // id column is LONG-typed — DataTable stores the string value converted to long
            Assert.Equal("1", Convert.ToString(dt.Rows[0]["id"]));
            Assert.Equal("hello", dt.Rows[0]["val"]);
        }

        // ── PENDING then SUCCEEDED ─────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_PendingThenSucceeded_Polls()
        {
            var pendingResponse = """
                {
                  "statement_id": "stmt-2",
                  "status": { "state": "PENDING" }
                }
                """;

            var (client, handler) = Build(
                (HttpStatusCode.OK, pendingResponse),
                (HttpStatusCode.OK, SucceededResponse("stmt-2")));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT 1");

            // POST + 1 poll GET
            Assert.Equal(2, handler.CapturedUrls.Count);
            Assert.Contains("/api/2.0/sql/statements/stmt-2", handler.CapturedUrls[1]);
            Assert.Equal(2, dt.Rows.Count);
        }

        // ── RUNNING then SUCCEEDED ─────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_RunningThenSucceeded_Polls()
        {
            var runningResponse = """
                {
                  "statement_id": "stmt-3",
                  "status": { "state": "RUNNING" }
                }
                """;

            var (client, _) = Build(
                (HttpStatusCode.OK, runningResponse),
                (HttpStatusCode.OK, SucceededResponse("stmt-3")));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT 1");
            Assert.Equal(2, dt.Rows.Count);
        }

        // ── FAILED state ───────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_FailedState_ThrowsDatabricksApiException()
        {
            var failedResponse = """
                {
                  "statement_id": "stmt-fail",
                  "status": {
                    "state": "FAILED",
                    "error": { "message": "Table not found", "error_code": "TABLE_NOT_FOUND" }
                  }
                }
                """;

            var (client, _) = Build((HttpStatusCode.OK, failedResponse));

            var ex = await Assert.ThrowsAsync<DatabricksApiException>(
                () => client.ExecuteAsync(WarehouseId, "SELECT * FROM missing"));

            Assert.Contains("Table not found", ex.Message);
            Assert.Equal("TABLE_NOT_FOUND", ex.ErrorCode);
        }

        // ── CANCELLED state ────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_CancelledState_ThrowsDatabricksApiException()
        {
            var cancelledResponse = """
                {
                  "statement_id": "stmt-cancel",
                  "status": { "state": "CANCELLED" }
                }
                """;

            var (client, _) = Build((HttpStatusCode.OK, cancelledResponse));

            await Assert.ThrowsAsync<DatabricksApiException>(
                () => client.ExecuteAsync(WarehouseId, "SELECT 1"));
        }

        // ── Multi-chunk response ───────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_MultiChunk_CombinesRows()
        {
            var firstResponse = """
                {
                  "statement_id": "stmt-chunk",
                  "status": { "state": "SUCCEEDED" },
                  "result": {
                    "schema": { "columns": [{"name":"n","type_name":"STRING"}] },
                    "data_array": [["row1"],["row2"]],
                    "next_chunk_index": 1
                  }
                }
                """;
            var chunkResponse = """
                {
                  "data_array": [["row3"]]
                }
                """;

            var (client, handler) = Build(
                (HttpStatusCode.OK, firstResponse),
                (HttpStatusCode.OK, chunkResponse));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT n FROM big_table");

            Assert.Equal(3, dt.Rows.Count);
            Assert.Equal("row1", dt.Rows[0]["n"]);
            Assert.Equal("row3", dt.Rows[2]["n"]);
            // Verify chunk URL was fetched
            Assert.Contains("/chunks/1", handler.CapturedUrls[1]);
        }

        // ── Column type mapping ────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_ColumnTypes_MappedCorrectly()
        {
            var response = """
                {
                  "statement_id": "stmt-types",
                  "status": { "state": "SUCCEEDED" },
                  "result": {
                    "schema": {
                      "columns": [
                        {"name":"long_col","type_name":"LONG"},
                        {"name":"bool_col","type_name":"BOOLEAN"},
                        {"name":"str_col","type_name":"STRING"}
                      ]
                    },
                    "data_array": []
                  }
                }
                """;

            var (client, _) = Build((HttpStatusCode.OK, response));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT 1");

            Assert.Equal(typeof(long), dt.Columns["long_col"]!.DataType);
            Assert.Equal(typeof(bool), dt.Columns["bool_col"]!.DataType);
            Assert.Equal(typeof(string), dt.Columns["str_col"]!.DataType);
        }

        // ── Empty result set ───────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_EmptyDataArray_ReturnsEmptyDataTable()
        {
            var response = """
                {
                  "statement_id": "stmt-empty",
                  "status": { "state": "SUCCEEDED" },
                  "result": {
                    "schema": { "columns": [{"name":"x","type_name":"STRING"}] },
                    "data_array": []
                  }
                }
                """;

            var (client, _) = Build((HttpStatusCode.OK, response));

            var dt = await client.ExecuteAsync(WarehouseId, "SELECT x FROM empty_table");

            Assert.Single(dt.Columns);
            Assert.Empty(dt.Rows);
        }
    }
}
