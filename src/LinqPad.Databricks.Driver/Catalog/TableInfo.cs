using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Catalog
{
    public class TableInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("schema_name")]
        public string SchemaName { get; set; } = string.Empty;

        [JsonPropertyName("catalog_name")]
        public string CatalogName { get; set; } = string.Empty;

        [JsonPropertyName("table_type")]
        public string TableType { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }
}
