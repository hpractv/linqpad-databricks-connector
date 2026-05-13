using System.Net;
using System.Text.Json;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Tests;

public class StatementExecutionClientTests
{
    // ─── Immediate SUCCEEDED ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ImmediateSucceeded_ReturnsResult()
    {
        string json = """
            {
              "statement_id": "abc-123",
              "status": {"state": "SUCCEEDED"},
              "manifest": {"schema": {"columns": [{"name":"id","type_name":"BIGINT"},{"name":"name","type_name":"STRING"}]}},
              "result": {"data_array": [["1","Alice"],["2","Bob"]]}
            }
            """;
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new StatementExecutionClient(http);

        StatementResult result = await client.ExecuteAsync("wh-001", "SELECT * FROM users");

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal("id", result.Columns[0].Name);
        Assert.Equal("BIGINT", result.Columns[0].TypeName);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("1", result.Rows[0][0]);
        Assert.Equal("Alice", result.Rows[0][1]);
    }

    // ─── Polling PENDING → SUCCEEDED ─────────────────────────────────────────

    [Fact]
    public async Task Execute_PendingThenSucceeded_PollsUntilComplete()
    {
        var responses = new[]
        {
            (HttpStatusCode.OK, """{"statement_id":"abc","status":{"state":"PENDING"}}"""),
            (HttpStatusCode.OK, """{"statement_id":"abc","status":{"state":"RUNNING"}}"""),
            (HttpStatusCode.OK, """
                {
                  "statement_id":"abc",
                  "status":{"state":"SUCCEEDED"},
                  "manifest":{"schema":{"columns":[{"name":"n","type_name":"INT"}]}},
                  "result":{"data_array":[["42"]]}
                }
            """),
        };

        var handler = new SequentialHandler(responses);
        using var http = new DatabricksHttpClient("https://adb-1.azuredatabricks.net", "token", handler);
        var client = new StatementExecutionClient(http);

        StatementResult result = await client.ExecuteAsync("wh-001", "SELECT 42");

        Assert.Equal(1, result.Columns.Count);
        Assert.Equal("n", result.Columns[0].Name);
        Assert.Equal("42", result.Rows[0][0]);
        Assert.Equal(3, handler.CallCount); // POST + 2 GET polls
    }

    // ─── FAILED state ────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_Failed_ThrowsDatabricksApiException()
    {
        string json = """
            {
              "statement_id":"abc",
              "status":{"state":"FAILED","error":{"error_code":"QUERY_ERROR","message":"Syntax error near SELECT"}}
            }
            """;
        using var http = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token",
            DatabricksHttpClientTests.CreateHandler(HttpStatusCode.OK, json));
        var client = new StatementExecutionClient(http);

        var ex = await Assert.ThrowsAsync<DatabricksApiException>(
            () => client.ExecuteAsync("wh-001", "BAD SQL"));

        Assert.Equal("QUERY_ERROR", ex.ErrorCode);
        Assert.Contains("Syntax error", ex.Message);
    }

    // ─── Multi-chunk result ───────────────────────────────────────────────────

    [Fact]
    public async Task Execute_MultiChunk_CollectsAllRows()
    {
        var responses = new[]
        {
            (HttpStatusCode.OK, """
                {
                  "statement_id":"abc",
                  "status":{"state":"SUCCEEDED"},
                  "manifest":{"schema":{"columns":[{"name":"v","type_name":"STRING"}]}},
                  "result":{"data_array":[["row1"],["row2"]],"next_chunk_index":1}
                }
            """),
            // GET /api/2.0/sql/statements/abc/result/chunks/1
            (HttpStatusCode.OK, """{"data_array":[["row3"],["row4"]]}"""),
        };

        var handler = new SequentialHandler(responses);
        using var http = new DatabricksHttpClient("https://adb-1.azuredatabricks.net", "token", handler);
        var client = new StatementExecutionClient(http);

        StatementResult result = await client.ExecuteAsync("wh-001", "SELECT v FROM t");

        Assert.Equal(4, result.Rows.Count);
        Assert.Equal("row1", result.Rows[0][0]);
        Assert.Equal("row4", result.Rows[3][0]);
    }

    // ─── ToDataTable ─────────────────────────────────────────────────────────

    [Fact]
    public void ToDataTable_ConvertsResult()
    {
        var result = new StatementResult
        {
            Columns = [new ColumnSchema("id", "INT"), new ColumnSchema("name", "STRING")],
            Rows = [new[] { "1", "Alice" }, new[] { "2", (string?)null }]
        };

        var dt = StatementExecutionClient.ToDataTable(result);

        Assert.Equal(2, dt.Columns.Count);
        Assert.Equal("id", dt.Columns[0].ColumnName);
        Assert.Equal(2, dt.Rows.Count);
        Assert.Equal("1", dt.Rows[0]["id"]);
        Assert.Equal(System.DBNull.Value, dt.Rows[1]["name"]);
    }
}
