using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Catalog
{
    public class SchemaInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("catalog_name")]
        public string CatalogName { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }
}
