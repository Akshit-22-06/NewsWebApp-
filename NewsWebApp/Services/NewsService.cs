using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsWebApp.Models;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Service for fetching live news and searching articles from Currents News API.
    /// Follows a simple, beginner-friendly pattern: Controller -> NewsService -> HttpClient -> Currents API.
    /// </summary>
    public class NewsService : INewsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewsService> _logger;

        public NewsService(
            HttpClient httpClient, 
            IConfiguration configuration,
            ILogger<NewsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the latest breaking news from Currents API.
        /// Supports optional category, country (e.g. "IN", "US", "GB"), and language (e.g. "en") filtering.
        /// </summary>
        public async Task<List<NewsDto>> GetLatestNewsAsync(string? category = null, string? country = null, string? language = null)
        {
            var queryParams = new Dictionary<string, string?>
            {
                { "language", string.IsNullOrWhiteSpace(language) ? "en" : language.Trim() },
                { "category", category?.Trim() },
                { "country", country?.Trim() }
            };

            string queryString = BuildQueryString(queryParams);
            string endpoint = $"v1/latest-news{queryString}";

            return await ExecuteCurrentsRequestAsync(endpoint);
        }

        /// <summary>
        /// Searches news articles matching a query term from Currents API.
        /// Supports optional category, country, and language filtering.
        /// </summary>
        public async Task<List<NewsDto>> SearchNewsAsync(string query, string? category = null, string? country = null, string? language = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await GetLatestNewsAsync(category, country, language);
            }

            var queryParams = new Dictionary<string, string?>
            {
                { "keywords", query.Trim() },
                { "language", string.IsNullOrWhiteSpace(language) ? "en" : language.Trim() },
                { "category", category?.Trim() },
                { "country", country?.Trim() }
            };

            string queryString = BuildQueryString(queryParams);
            string endpoint = $"v1/search{queryString}";

            return await ExecuteCurrentsRequestAsync(endpoint);
        }

        /// <summary>
        /// Retrieves latest news for a specific category (e.g. "technology", "business", "sports", etc.).
        /// </summary>
        public async Task<List<NewsDto>> GetNewsByCategoryAsync(string category)
        {
            return await GetLatestNewsAsync(category: category);
        }

        /// <summary>
        /// Sends an authenticated GET request to the Currents API endpoint and maps results to NewsDto.
        /// </summary>
        private async Task<List<NewsDto>> ExecuteCurrentsRequestAsync(string relativeUrl)
        {
            string? apiKey = _configuration["CurrentsApi:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Currents API key is not configured in appsettings or user secrets.");
                throw new HttpRequestException("Currents API key is missing. Please configure 'CurrentsApi:ApiKey'.");
            }

            // Create request message with standard Bearer authorization header
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Currents API returned status code {StatusCode} for URL {Url}", response.StatusCode, relativeUrl);
                throw new HttpRequestException($"Currents API returned HTTP {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var apiResult = JsonSerializer.Deserialize<CurrentsApiResponse>(json, options);

            var resultList = new List<NewsDto>();
            if (apiResult?.News == null)
            {
                return resultList;
            }

            foreach (var raw in apiResult.News)
            {
                // Safely parse published timestamp
                DateTime? publishedDate = null;
                if (!string.IsNullOrWhiteSpace(raw.Published) && DateTime.TryParse(raw.Published, out var dt))
                {
                    publishedDate = dt;
                }

                // Handle image property safely (Currents API sometimes returns the string "None" when no image is available)
                string? image = raw.Image;
                if (string.Equals(image, "None", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(image))
                {
                    image = null;
                }

                // Handle author safely
                string? author = raw.Author;
                if (string.Equals(author, "None", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(author))
                {
                    author = null;
                }

                resultList.Add(new NewsDto
                {
                    Id = raw.Id,
                    Title = raw.Title,
                    Description = raw.Description,
                    Url = raw.Url,
                    Image = image,
                    Author = author,
                    Published = publishedDate,
                    Category = raw.Category != null && raw.Category.Count > 0 ? string.Join(", ", raw.Category) : null,
                    Language = raw.Language
                });
            }

            return resultList;
        }

        /// <summary>
        /// Helper to construct URL query strings cleanly from key-value pairs.
        /// </summary>
        private static string BuildQueryString(Dictionary<string, string?> parameters)
        {
            var parts = new List<string>();
            foreach (var kvp in parameters)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    parts.Add($"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}");
                }
            }
            return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
        }
    }
}
