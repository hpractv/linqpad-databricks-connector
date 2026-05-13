using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Sql
{
    internal class StatementRequest
    {
        [JsonPropertyName("warehouse_id")]
        public string WarehouseId { get; set; } = string.Empty;

        [JsonPropertyName("statement")]
        public string Statement { get; set; } = string.Empty;

        [JsonPropertyName("wait_timeout")]
        public string WaitTimeout { get; set; } = "30s";

        [JsonPropertyName("on_wait_timeout")]
        public string OnWaitTimeout { get; set; } = "CONTINUE";

        [JsonPropertyName("disposition")]
        public string Disposition { get; set; } = "INLINE";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "JSON_ARRAY";
    }
}
