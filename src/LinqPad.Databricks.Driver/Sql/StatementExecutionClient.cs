using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Driver.Sql
{
    public class StatementExecutionClient
    {
        private static readonly TimeSpan[] PollDelays = new[]
        {
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(2000),
        };

        private static readonly string[] TerminalStates = { "SUCCEEDED", "FAILED", "CANCELLED", "CLOSED" };

        private readonly DatabricksHttpClient _http;

        public StatementExecutionClient(DatabricksHttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<DataTable> ExecuteAsync(string warehouseId, string sql, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(warehouseId))
                throw new ArgumentException("warehouseId is required", nameof(warehouseId));
            if (sql is null)
                throw new ArgumentNullException(nameof(sql));

            var request = new StatementRequest
            {
                WarehouseId = warehouseId,
                Statement = sql
            };

            var response = await _http.PostAsync<StatementResponse>(
                "/api/2.0/sql/statements", request, ct).ConfigureAwait(false);

            if (response is null)
                throw new InvalidOperationException("Received null response from statement execution API.");

            // Poll until terminal state
            var statementId = response.StatementId;
            var pollDelayIndex = 0;

            while (!IsTerminal(response.Status?.State))
            {
                var delay = PollDelays[Math.Min(pollDelayIndex++, PollDelays.Length - 1)];
                await Task.Delay(delay, ct).ConfigureAwait(false);

                response = await _http.GetAsync<StatementResponse>(
                    $"/api/2.0/sql/statements/{statementId}", ct).ConfigureAwait(false);

                if (response is null)
                    throw new InvalidOperationException("Received null poll response.");
            }

            var state = response.Status?.State ?? "";

            if (state != "SUCCEEDED")
            {
                var errMsg = response.Status?.Error?.Message ?? $"Statement ended with state: {state}";
                var errCode = response.Status?.Error?.ErrorCode;
                throw new DatabricksApiException(errMsg, errCode, 0);
            }

            return BuildDataTable(statementId, response.Result, ct);
        }

        private DataTable BuildDataTable(string statementId, StatementResult? result, CancellationToken ct)
        {
            var dt = new DataTable();

            if (result?.Schema?.Columns is null)
                return dt;

            // Add columns
            foreach (var col in result.Schema.Columns)
                dt.Columns.Add(col.Name, MapType(col.TypeName));

            // Add rows from first chunk
            AppendRows(dt, result.DataArray);

            // Fetch additional chunks if present
            var nextChunk = result.NextChunkIndex;
            while (nextChunk.HasValue)
            {
                var chunk = _http.GetAsync<ChunkResponse>(
                    $"/api/2.0/sql/statements/{statementId}/result/chunks/{nextChunk.Value}", ct)
                    .GetAwaiter().GetResult();

                AppendRows(dt, chunk?.DataArray);
                nextChunk = chunk?.NextChunkIndex;
            }

            return dt;
        }

        private static void AppendRows(DataTable dt, System.Collections.Generic.List<System.Collections.Generic.List<string?>>? rows)
        {
            if (rows is null) return;
            foreach (var row in rows)
            {
                var dataRow = dt.NewRow();
                for (int i = 0; i < dt.Columns.Count && i < row.Count; i++)
                {
                    var raw = row[i];
                    dataRow[i] = raw is null ? DBNull.Value : (object)raw;
                }
                dt.Rows.Add(dataRow);
            }
        }

        private static Type MapType(string typeName) => typeName?.ToUpperInvariant() switch
        {
            "INT" or "INTEGER" or "INT32" => typeof(int),
            "BIGINT" or "LONG" or "INT64" => typeof(long),
            "SMALLINT" or "SHORT" or "INT16" => typeof(short),
            "TINYINT" or "BYTE" => typeof(byte),
            "FLOAT" or "REAL" => typeof(float),
            "DOUBLE" or "DECIMAL" or "NUMERIC" => typeof(double),
            "BOOLEAN" or "BOOL" => typeof(bool),
            _ => typeof(string)
        };

        private static bool IsTerminal(string? state)
        {
            if (state is null) return false;
            foreach (var s in TerminalStates)
                if (string.Equals(s, state, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
