using System.Data;
using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Client;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record ColumnSchema(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type_name")] string TypeName
);

public sealed class StatementResult
{
    public IReadOnlyList<ColumnSchema> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = [];
}

internal sealed class StatementStatusState
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Canceled = "CANCELED";
    public const string Closed = "CLOSED";
}

internal sealed class StatementStatusError
{
    [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

internal sealed class StatementStatus
{
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("error")] public StatementStatusError? Error { get; set; }
}

internal sealed class ManifestColumn
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type_name")] public string TypeName { get; set; } = string.Empty;
}

internal sealed class ManifestSchema
{
    [JsonPropertyName("columns")] public List<ManifestColumn>? Columns { get; set; }
}

internal sealed class StatementManifest
{
    [JsonPropertyName("schema")] public ManifestSchema? Schema { get; set; }
    [JsonPropertyName("total_row_count")] public long? TotalRowCount { get; set; }
}

internal sealed class StatementResultData
{
    [JsonPropertyName("data_array")] public List<List<string?>>? DataArray { get; set; }
    [JsonPropertyName("next_chunk_index")] public int? NextChunkIndex { get; set; }
    [JsonPropertyName("next_chunk_internal_link")] public string? NextChunkInternalLink { get; set; }
}

internal sealed class StatementResponse
{
    [JsonPropertyName("statement_id")] public string StatementId { get; set; } = string.Empty;
    [JsonPropertyName("status")] public StatementStatus Status { get; set; } = new();
    [JsonPropertyName("manifest")] public StatementManifest? Manifest { get; set; }
    [JsonPropertyName("result")] public StatementResultData? Result { get; set; }
}

internal sealed class ChunkResponse
{
    [JsonPropertyName("data_array")] public List<List<string?>>? DataArray { get; set; }
    [JsonPropertyName("next_chunk_index")] public int? NextChunkIndex { get; set; }
}

// ─── Client ───────────────────────────────────────────────────────────────────

public class StatementExecutionClient
{
    private const string StatementsBase = "api/2.0/sql/statements";

    private readonly DatabricksHttpClient _http;

    public StatementExecutionClient(DatabricksHttpClient http) => _http = http;

    public async Task<StatementResult> ExecuteAsync(string warehouseId, string sql, CancellationToken ct = default)
    {
        var request = new
        {
            warehouse_id = warehouseId,
            statement = sql,
            wait_timeout = "30s",
            on_wait_timeout = "CANCEL",
            disposition = "INLINE",
            format = "JSON_ARRAY",
        };

        StatementResponse response = await _http.PostAsync<StatementResponse>(StatementsBase, request, ct)
            .ConfigureAwait(false);

        response = await WaitForCompletionAsync(response, ct).ConfigureAwait(false);

        return await MaterializeResultAsync(response, ct).ConfigureAwait(false);
    }

    private async Task<StatementResponse> WaitForCompletionAsync(StatementResponse response, CancellationToken ct)
    {
        TimeSpan delay = TimeSpan.FromMilliseconds(500);
        TimeSpan maxDelay = TimeSpan.FromSeconds(5);

        while (response.Status.State is StatementStatusState.Pending or StatementStatusState.Running)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));
            response = await _http.GetAsync<StatementResponse>($"{StatementsBase}/{response.StatementId}", ct)
                .ConfigureAwait(false);
        }

        return response;
    }

    private async Task<StatementResult> MaterializeResultAsync(StatementResponse response, CancellationToken ct)
    {
        if (response.Status.State is StatementStatusState.Failed
            or StatementStatusState.Canceled
            or StatementStatusState.Closed)
        {
            string errorCode = response.Status.Error?.ErrorCode ?? response.Status.State;
            string message = response.Status.Error?.Message ?? $"Statement ended with state: {response.Status.State}";
            throw new DatabricksApiException(errorCode, message, System.Net.HttpStatusCode.OK);
        }

        // Extract column schema from manifest
        var columns = (response.Manifest?.Schema?.Columns ?? [])
            .Select(c => new ColumnSchema(c.Name, c.TypeName))
            .ToList();

        // Collect all rows (first chunk + additional chunks)
        var rows = new List<IReadOnlyList<string?>>();

        if (response.Result?.DataArray != null)
            rows.AddRange(response.Result.DataArray.Select(r => (IReadOnlyList<string?>)r));

        // Fetch additional chunks if available
        int? nextChunk = response.Result?.NextChunkIndex;
        while (nextChunk.HasValue)
        {
            ChunkResponse chunk = await _http.GetAsync<ChunkResponse>(
                $"{StatementsBase}/{response.StatementId}/result/chunks/{nextChunk.Value}", ct)
                .ConfigureAwait(false);

            if (chunk.DataArray != null)
                rows.AddRange(chunk.DataArray.Select(r => (IReadOnlyList<string?>)r));

            nextChunk = chunk.NextChunkIndex;
        }

        return new StatementResult { Columns = columns, Rows = rows };
    }

    public static DataTable ToDataTable(StatementResult result)
    {
        var table = new DataTable();
        foreach (ColumnSchema col in result.Columns)
            table.Columns.Add(col.Name, typeof(string));

        foreach (IReadOnlyList<string?> row in result.Rows)
        {
            DataRow dataRow = table.NewRow();
            for (int i = 0; i < result.Columns.Count; i++)
                dataRow[i] = row.Count > i ? (object?)row[i] ?? DBNull.Value : DBNull.Value;
            table.Rows.Add(dataRow);
        }
        return table;
    }
}
