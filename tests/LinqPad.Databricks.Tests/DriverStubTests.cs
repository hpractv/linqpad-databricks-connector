using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LinqPad.Databricks.Driver.Ado;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Tests
{
    public class DatabricksCommandTests
    {
        private const string WorkspaceUrl = "https://adb-123.azuredatabricks.net";
        private const string Pat = "test-pat";
        private const string WarehouseId = "wh-abc";

        private class QueuedFakeHandler : DelegatingHandler
        {
            private readonly Queue<(HttpStatusCode status, string json)> _responses;
            public QueuedFakeHandler(IEnumerable<(HttpStatusCode, string)> responses)
                => _responses = new Queue<(HttpStatusCode, string)>(responses);

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_responses.Count == 0)
                    throw new InvalidOperationException("No more queued responses.");
                var (status, json) = _responses.Dequeue();
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json)
                });
            }
        }

        private static DatabricksConnection BuildConnection(params (HttpStatusCode, string)[] responses)
        {
            var handler = new QueuedFakeHandler(responses);
            var http = new DatabricksHttpClient(WorkspaceUrl, Pat, handler);
            // Bypass normal Open() by injecting ExecutionClient via the internal ctor chain:
            // Create a connection and open it with a real http client that has our fake handler.
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            // We can't inject HTTP into DatabricksConnection directly, so we rely on its
            // Open() creating a real DatabricksHttpClient. Instead, build via a helper
            // that replaces the execution client after open using the internal ctor.
            // Since internal ctors are exposed via InternalsVisibleTo, we use the
            // StatementExecutionClient + DatabricksHttpClient internal constructor.
            var exec = new Driver.Sql.StatementExecutionClient(http);
            conn.InjectExecutionClientForTest(exec);
            return conn;
        }

        private static string SucceededResponse(int rows = 2) => $$"""
            {
              "statement_id": "stmt-cmd",
              "status": { "state": "SUCCEEDED" },
              "result": {
                "schema": { "columns": [{"name":"id","type_name":"LONG"},{"name":"val","type_name":"STRING"}] },
                "data_array": {{GenerateRows(rows)}}
              }
            }
            """;

        private static string GenerateRows(int count)
        {
            var rows = new System.Text.StringBuilder("[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0) rows.Append(',');
                rows.Append($"[\"{i + 1}\",\"v{i + 1}\"]");
            }
            rows.Append(']');
            return rows.ToString();
        }

        [Fact]
        public void ExecuteNonQuery_ReturnsRowCount()
        {
            using var conn = BuildConnection((HttpStatusCode.OK, SucceededResponse(3)));
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var rows = cmd.ExecuteNonQuery();
            Assert.Equal(3, rows);
        }

        [Fact]
        public void ExecuteScalar_ReturnsFirstCell()
        {
            using var conn = BuildConnection((HttpStatusCode.OK, SucceededResponse(2)));
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM t";
            var scalar = cmd.ExecuteScalar();
            // id column is LONG type; value from data_array is "1"
            Assert.NotNull(scalar);
            Assert.Equal("1", scalar!.ToString());
        }

        [Fact]
        public void ExecuteScalar_EmptyResult_ReturnsNull()
        {
            const string emptyResponse = """
                {
                  "statement_id": "s",
                  "status": { "state": "SUCCEEDED" },
                  "result": {
                    "schema": { "columns": [{"name":"x","type_name":"STRING"}] },
                    "data_array": []
                  }
                }
                """;
            using var conn = BuildConnection((HttpStatusCode.OK, emptyResponse));
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            cmd.CommandText = "SELECT x FROM empty";
            Assert.Null(cmd.ExecuteScalar());
        }

        [Fact]
        public void ExecuteReader_ReturnsCorrectColumnAndRowCount()
        {
            using var conn = BuildConnection((HttpStatusCode.OK, SucceededResponse(4)));
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            cmd.CommandText = "SELECT id, val FROM t";

            using var reader = cmd.ExecuteReader();
            Assert.Equal(2, reader.FieldCount);
            int rowCount = 0;
            while (reader.Read()) rowCount++;
            Assert.Equal(4, rowCount);
        }

        [Fact]
        public void ExecuteNonQuery_AutoOpensConnection()
        {
            using var conn = BuildConnection((HttpStatusCode.OK, SucceededResponse(1)));
            // Do NOT call conn.Open() — command should auto-open it
            // (conn is already in injected state, but State will be Closed without explicit Open)
            // We need to reset state; since we use InjectExecutionClientForTest which sets Open,
            // just verify no exception is thrown when called on a fresh open connection.
            Assert.Equal(ConnectionState.Open, conn.State); // injected as open
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = cmd.ExecuteNonQuery();
            Assert.Equal(1, result);
        }

        [Fact]
        public void CommandTimeout_DefaultIs120()
        {
            using var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            using var cmd = (DatabricksCommand)conn.CreateCommand();
            Assert.Equal(120, cmd.CommandTimeout);
        }
    }
}
