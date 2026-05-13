using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LinqPad.Databricks.Driver.Models
{
    public class PagedResponse<T>
    {
        [JsonPropertyName("items")]
        public List<T> Items { get; set; } = new List<T>();

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; set; }
    }
}
