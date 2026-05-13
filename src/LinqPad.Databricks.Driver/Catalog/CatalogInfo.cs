using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Catalog
{
    public class CatalogInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }
}
