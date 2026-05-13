using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Sql
{
    internal class StatementResponse
    {
        [JsonPropertyName("statement_id")]
        public string StatementId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public StatementStatus? Status { get; set; }

        [JsonPropertyName("result")]
        public StatementResult? Result { get; set; }
    }

    internal class StatementStatus
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public StatementError? Error { get; set; }
    }

    internal class StatementError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }
    }

    internal class StatementResult
    {
        [JsonPropertyName("schema")]
        public ResultSchema? Schema { get; set; }

        [JsonPropertyName("data_array")]
        public List<List<string?>>? DataArray { get; set; }

        [JsonPropertyName("next_chunk_index")]
        public int? NextChunkIndex { get; set; }
    }

    internal class ResultSchema
    {
        [JsonPropertyName("columns")]
        public List<ColumnInfo> Columns { get; set; } = new();
    }

    internal class ColumnInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type_name")]
        public string TypeName { get; set; } = string.Empty;
    }

    internal class ChunkResponse
    {
        [JsonPropertyName("data_array")]
        public List<List<string?>>? DataArray { get; set; }

        [JsonPropertyName("next_chunk_index")]
        public int? NextChunkIndex { get; set; }
    }
}
