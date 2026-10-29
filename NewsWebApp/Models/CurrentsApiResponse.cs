using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Response model returned by Currents API endpoints (/v1/latest-news, /v1/search).
    /// </summary>
    public class CurrentsApiResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("news")]
        public List<CurrentsArticleRaw>? News { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }
    }

    /// <summary>
    /// Raw article structure returned by the Currents API JSON payload.
    /// </summary>
    public class CurrentsArticleRaw
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("category")]
        public List<string>? Category { get; set; }

        [JsonPropertyName("published")]
        public string? Published { get; set; }
    }
}
