using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Catalog
{
    public class ColumnInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type_name")]
        public string TypeName { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("nullable")]
        public bool Nullable { get; set; } = true;

        [JsonPropertyName("position")]
        public int Position { get; set; }
    }
}
