using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsWebApp.Models;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Service for fetching live news, trending stories, and searching articles from Currents News API.
    /// Follows a clean, beginner-friendly pattern: Controller -> NewsService -> HttpClient -> Currents API.
    /// Includes 15-minute in-memory caching and strict 24-hour freshness verification.
    /// </summary>
    public class NewsService : INewsService
    {
        private const string TrendingCacheKey = "NewsBase_TrendingNewsToday_LiveCache";
        private static readonly TimeSpan TrendingCacheDuration = TimeSpan.FromMinutes(15);

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NewsService> _logger;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "after", "about", "their", "there", "which", "would", "could", "these", "those",
            "where", "while", "under", "nearly", "years", "state", "first", "order", "latest",
            "today", "report", "breaking", "update", "says", "told", "into", "over", "from",
            "with", "this", "that", "have", "been", "were", "will", "more", "than", "other",
            "also", "when", "what", "some", "time", "just", "back", "then", "them"
        };

        public NewsService(
            HttpClient httpClient, 
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<NewsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
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
        /// Retrieves genuine, fresh trending news stories of TODAY directly from Currents API.
        /// Strictly excludes articles older than the last 24 hours.
        /// Ranks articles dynamically based on recency, multi-source consensus, and topic importance.
        /// Cached for 15 minutes to respect API rate limits. Never falls back to old database news.
        /// </summary>
        public async Task<List<TrendingArticleDto>> GetTrendingNewsTodayAsync(int count = 5)
        {
            var nowUtc = DateTime.UtcNow;
            var cutoff24h = nowUtc.AddHours(-24);

            // 1. Check in-memory cache
            if (_cache.TryGetValue(TrendingCacheKey, out List<TrendingArticleDto>? cached) && cached != null && cached.Count > 0)
            {
                // Verify all cached articles are still within the 24-hour freshness window
                var freshCached = cached
                    .Where(a => a.PublishedDate.HasValue && a.PublishedDate.Value >= cutoff24h)
                    .ToList();

                if (freshCached.Count >= count)
                {
                    _logger.LogInformation("Serving {Count} trending stories from 15-minute memory cache.", freshCached.Count);
                    return freshCached.Take(count).ToList();
                }
            }

            // 2. Fetch fresh candidates from Currents API
            var rawCandidates = new List<CurrentsArticleRaw>();
            try
            {
                // Primary query: Fresh news focused on India (country=IN, language=en)
                var inNews = await FetchRawArticlesAsync("v1/latest-news?country=IN&language=en");
                rawCandidates.AddRange(inNews);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch India latest news from Currents API.");
            }

            try
            {
                // Secondary query: Global fresh breaking headlines for category diversity (technology, science, world)
                var globalNews = await FetchRawArticlesAsync("v1/latest-news?language=en");
                rawCandidates.AddRange(globalNews);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch Global latest news from Currents API.");
            }

            // 3. Strictly filter for valid, fresh articles within the last 24 hours
            var validArticles = new List<CandidateArticle>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in rawCandidates)
            {
                if (string.IsNullOrWhiteSpace(raw.Title) || string.IsNullOrWhiteSpace(raw.Url))
                    continue;

                // Validate publisher URL
                if (!Uri.TryCreate(raw.Url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    continue;

                // Validate publication date
                if (string.IsNullOrWhiteSpace(raw.Published) || !DateTimeOffset.TryParse(raw.Published, out var dto))
                    continue;

                var pubUtc = dto.UtcDateTime;
                // Exclude articles older than 24 hours or with invalid future dates (> 2h ahead)
                if (pubUtc < cutoff24h || pubUtc > nowUtc.AddHours(2))
                    continue;

                var normalizedTitle = NormalizeTitle(raw.Title);
                if (normalizedTitle.Length < 10)
                    continue;

                if (!seenUrls.Add(raw.Url) || !seenTitles.Add(normalizedTitle))
                    continue;

                var host = uri.Host.ToLowerInvariant();
                var keywords = ExtractKeywords(raw.Title);
                var category = MapCategory(raw.Category, raw.Title);
                var sourceName = ExtractPublisherName(raw.Url, raw.Author);

                validArticles.Add(new CandidateArticle
                {
                    Raw = raw,
                    Title = raw.Title.Trim(),
                    Url = raw.Url.Trim(),
                    Host = host,
                    SourceName = sourceName,
                    CategoryName = category,
                    PublishedUtc = pubUtc,
                    Keywords = keywords
                });
            }

            // If no fresh news found in last 24 hours, return empty list (UI will render proper empty state)
            if (validArticles.Count == 0)
            {
                _logger.LogWarning("Currents API returned no articles within the last 24 hours. Returning empty trending list.");
                return new List<TrendingArticleDto>();
            }

            // 4. Multi-Source Story Clustering (detect same story covered across independent news outlets)
            foreach (var a in validArticles)
            {
                var coveringHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a.Host };
                var relatedArticles = new List<CandidateArticle> { a };

                foreach (var b in validArticles)
                {
                    if (ReferenceEquals(a, b)) continue;

                    // Check token overlap in headline keywords
                    int overlapCount = a.Keywords.Count(k => b.Keywords.Contains(k));
                    if (overlapCount >= 2)
                    {
                        relatedArticles.Add(b);
                        if (!string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase))
                        {
                            coveringHosts.Add(b.Host);
                        }
                    }
                }

                a.IndependentSourceCount = coveringHosts.Count;
                a.RelatedArticles = relatedArticles;

                // 5. Calculate Real Editorial Trending Score:
                // Recency: 0 hours = 100 points, linearly decreases to 0 at 25 hours
                double ageHours = Math.Max(0.0, (nowUtc - a.PublishedUtc).TotalHours);
                double recencyScore = Math.Max(0.0, 100.0 - (ageHours * 4.0));

                // Multi-source consensus boost: +35 points per additional independent newsroom reporting the story
                double clusterBoost = (a.IndependentSourceCount - 1) * 35.0;

                a.Score = recencyScore + clusterBoost;
            }

            // 6. Sort candidate articles by trending score
            var sortedCandidates = validArticles.OrderByDescending(a => a.Score).ToList();

            // 7. Select top 5 articles ensuring topic de-duplication and category diversity
            var selected = new List<CandidateArticle>();
            var coveredArticleUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var coveredCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First pass: Prioritize highest scoring articles covering distinct categories and distinct stories
            foreach (var a in sortedCandidates)
            {
                if (coveredArticleUrls.Contains(a.Url)) continue;

                if (!coveredCategories.Contains(a.CategoryName))
                {
                    selected.Add(a);
                    coveredCategories.Add(a.CategoryName);

                    // Mark all related articles in this story cluster as covered to prevent duplicate reporting
                    foreach (var rel in a.RelatedArticles)
                    {
                        coveredArticleUrls.Add(rel.Url);
                    }

                    if (selected.Count == count) break;
                }
            }

            // Second pass: If fewer than count (e.g. 5) selected, fill remaining slots with highest scoring unique stories
            if (selected.Count < count)
            {
                foreach (var a in sortedCandidates)
                {
                    if (coveredArticleUrls.Contains(a.Url)) continue;

                    selected.Add(a);
                    foreach (var rel in a.RelatedArticles)
                    {
                        coveredArticleUrls.Add(rel.Url);
                    }

                    if (selected.Count == count) break;
                }
            }

            // 8. Map to TrendingArticleDto
            var result = selected.Select(a =>
            {
                string reason = a.IndependentSourceCount > 1
                    ? $"Breaking coverage reported by {a.IndependentSourceCount} independent outlets"
                    : $"Breaking {a.CategoryName} headline published {FormatTimeAgo(a.PublishedUtc)}";

                return new TrendingArticleDto
                {
                    Title = a.Title,
                    Url = a.Url,
                    CategoryName = a.CategoryName,
                    CategorySlug = a.CategoryName.ToLowerInvariant().Replace(" ", "-"),
                    SourceName = a.SourceName,
                    PublishedDate = a.PublishedUtc,
                    FormattedTimeAgo = FormatTimeAgo(a.PublishedUtc),
                    TrendingReason = reason
                };
            }).ToList();

            // 9. Cache fresh results for 15 minutes
            if (result.Count > 0)
            {
                _cache.Set(TrendingCacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TrendingCacheDuration
                });
                _logger.LogInformation("Cached {Count} fresh trending news stories for 15 minutes.", result.Count);
            }

            return result;
        }

        /// <summary>
        /// Sends an authenticated GET request to the Currents API endpoint and maps results to NewsDto.
        /// </summary>
        private async Task<List<NewsDto>> ExecuteCurrentsRequestAsync(string relativeUrl)
        {
            var rawList = await FetchRawArticlesAsync(relativeUrl);
            var resultList = new List<NewsDto>();

            foreach (var raw in rawList)
            {
                DateTime? publishedDate = null;
                if (!string.IsNullOrWhiteSpace(raw.Published) && DateTimeOffset.TryParse(raw.Published, out var dto))
                {
                    publishedDate = dto.UtcDateTime;
                }

                string? image = raw.Image;
                if (string.Equals(image, "None", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(image))
                {
                    image = null;
                }

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
        /// Executes an authenticated GET request to Currents API and parses raw articles.
        /// </summary>
        private async Task<List<CurrentsArticleRaw>> FetchRawArticlesAsync(string relativeUrl)
        {
            string? apiKey = _configuration["CurrentsApi:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Currents API key is not configured in appsettings or user secrets.");
                throw new HttpRequestException("Currents API key is missing. Please configure 'CurrentsApi:ApiKey'.");
            }

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

            return apiResult?.News ?? new List<CurrentsArticleRaw>();
        }

        /// <summary>
        /// Maps raw Currents API category tags and title keywords to official major news categories.
        /// </summary>
        private static string MapCategory(List<string>? categories, string title)
        {
            var raw = (categories ?? new List<string>())
                .Select(c => c.ToLowerInvariant())
                .ToList();

            var titleLower = title.ToLowerInvariant();

            // Technology
            if (raw.Any(c => c is "technology" or "programming" or "gadgets") ||
                Regex.IsMatch(titleLower, @"\b(ai|artificial intelligence|tech|chip|nvidia|google|apple|microsoft|cyber|software)\b"))
            {
                return "Technology";
            }

            // Business & Finance
            if (raw.Any(c => c is "business" or "finance" or "economy" or "trading" or "commodity") ||
                Regex.IsMatch(titleLower, @"\b(market|stock|shares|inflation|rbi|bank|gdp|profit|revenue|investor)\b"))
            {
                return "Business";
            }

            // Politics
            if (raw.Any(c => c is "politics") ||
                Regex.IsMatch(titleLower, @"\b(bjp|congress|parliament|election|minister|modi|rahul|priyanka|rally|government)\b"))
            {
                return "Politics";
            }

            // Sports
            if (raw.Any(c => c is "sports" or "game") ||
                Regex.IsMatch(titleLower, @"\b(badminton|cricket|football|cup|match|win|title|olympics|champion|beats|score|tournament)\b"))
            {
                return "Sports";
            }

            // Entertainment
            if (raw.Any(c => c is "entertainment" or "movie" or "celebrity" or "music") ||
                Regex.IsMatch(titleLower, @"\b(actor|actress|film|movie|concert|bollywood|hollywood|ott|series)\b"))
            {
                return "Entertainment";
            }

            // Science
            if (raw.Any(c => c is "science" or "medical" or "environment") ||
                Regex.IsMatch(titleLower, @"\b(nasa|space|mars|satellite|climate|scientist|discovery|physics|research)\b"))
            {
                return "Science";
            }

            // World News
            if (raw.Any(c => c is "world") ||
                Regex.IsMatch(titleLower, @"\b(un|united nations|war|ukraine|russia|israel|gaza|china|us|turkey|iran|global)\b"))
            {
                return "World News";
            }

            // Default to India News for regional or domestic coverage
            return "India News";
        }

        /// <summary>
        /// Extracts clean, readable publisher source name from URL domain and author metadata.
        /// </summary>
        private static string ExtractPublisherName(string? url, string? author)
        {
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();
                if (host.Contains("economictimes")) return "The Economic Times";
                if (host.Contains("timesofindia")) return "The Times of India";
                if (host.Contains("indianexpress")) return "The Indian Express";
                if (host.Contains("indiatoday")) return "India Today";
                if (host.Contains("news18")) return "News18";
                if (host.Contains("thehindu")) return "The Hindu";
                if (host.Contains("hindustantimes")) return "Hindustan Times";
                if (host.Contains("aljazeera")) return "Al Jazeera";
                if (host.Contains("reuters")) return "Reuters";
                if (host.Contains("apnews")) return "Associated Press";
                if (host.Contains("bbc.")) return "BBC News";
                if (host.Contains("bloomberg")) return "Bloomberg";
                if (host.Contains("ndtv")) return "NDTV";
                if (host.Contains("livemint")) return "Mint";
                if (host.Contains("financialexpress")) return "Financial Express";
                if (host.Contains("winnipegfreepress")) return "Winnipeg Free Press";
                if (host.Contains("eurohoops")) return "Eurohoops";

                // Strip www. and domain suffix (e.g. "techcrunch.com" -> "Techcrunch")
                var cleanHost = host.Replace("www.", "").Split('.')[0];
                if (!string.IsNullOrWhiteSpace(cleanHost) && cleanHost.Length > 2)
                {
                    return char.ToUpperInvariant(cleanHost[0]) + cleanHost.Substring(1);
                }
            }

            if (!string.IsNullOrWhiteSpace(author) && !author.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                if (author.Contains("Associated Press", StringComparison.OrdinalIgnoreCase)) return "Associated Press";
                if (author.Contains("Reuters", StringComparison.OrdinalIgnoreCase)) return "Reuters";
                if (author.Contains("PTI", StringComparison.OrdinalIgnoreCase)) return "PTI";
                if (author.Contains("ET Online", StringComparison.OrdinalIgnoreCase)) return "The Economic Times";
                return author.Split(';', ',')[0].Trim();
            }

            return "Wire Service";
        }

        /// <summary>
        /// Formats publication elapsed time into a clean human-readable relative label (e.g. "15m ago", "2h ago").
        /// </summary>
        private static string FormatTimeAgo(DateTime pubUtc)
        {
            var age = DateTime.UtcNow - pubUtc;
            if (age.TotalMinutes < 1) return "Just now";
            if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
            if (age.TotalHours < 24) return $"{(int)age.TotalHours}h ago";
            return "1d ago";
        }

        private static string NormalizeTitle(string title)
        {
            return Regex.Replace(title.ToLowerInvariant(), @"[^\w\s]", "").Trim();
        }

        private static HashSet<string> ExtractKeywords(string title)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matches = Regex.Matches(title, @"[a-zA-Z]{4,}");
            foreach (Match m in matches)
            {
                var val = m.Value.ToLowerInvariant();
                if (!StopWords.Contains(val))
                {
                    words.Add(val);
                }
            }
            return words;
        }

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

        private class CandidateArticle
        {
            public CurrentsArticleRaw Raw { get; set; } = new();
            public string Title { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string Host { get; set; } = string.Empty;
            public string SourceName { get; set; } = string.Empty;
            public string CategoryName { get; set; } = string.Empty;
            public DateTime PublishedUtc { get; set; }
            public HashSet<string> Keywords { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public int IndependentSourceCount { get; set; } = 1;
            public double Score { get; set; }
            public List<CandidateArticle> RelatedArticles { get; set; } = new();
        }
    }
}
