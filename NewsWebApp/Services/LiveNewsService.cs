using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewsWebApp.Data;
using NewsWebApp.Models;
using NewsWebApp.Utils;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Simple and robust service that fetches live news from external APIs and RSS feeds.
    /// Supports Worldwide, Indian National, and State/City/District (Delhi, Mumbai, Bengaluru, etc.) news.
    /// Uses high-availability REST JSON parsing with direct XML fallback.
    /// </summary>
    public class LiveNewsService : ILiveNewsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LiveNewsService> _logger;

        // Configured live feeds across Worldwide, Indian National, and City/District local sources
        private static readonly List<LiveNewsFeedSource> DefaultFeeds = new()
        {
            // 1. Worldwide / Global Feeds
            new() { 
                Name = "BBC World", 
                CategorySlug = "world", 
                FeedUrl = "https://feeds.bbci.co.uk/news/world/rss.xml", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=800",
                Region = "Worldwide",
                LocationScope = "Global"
            },
            new() { 
                Name = "TechCrunch", 
                CategorySlug = "technology", 
                FeedUrl = "https://techcrunch.com/feed/", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=800",
                Region = "Worldwide",
                LocationScope = "Global"
            },
            new() { 
                Name = "BBC Business", 
                CategorySlug = "business", 
                FeedUrl = "https://feeds.bbci.co.uk/news/business/rss.xml", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1590283603385-17ffb3a7f29f?w=800",
                Region = "Worldwide",
                LocationScope = "Global"
            },
            new() { 
                Name = "BBC Science", 
                CategorySlug = "science", 
                FeedUrl = "https://feeds.bbci.co.uk/news/science_and_environment/rss.xml", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1473341304170-971dccb5ac1e?w=800",
                Region = "Worldwide",
                LocationScope = "Global"
            },

            // 2. India - National Feeds
            new() { 
                Name = "The Hindu National", 
                CategorySlug = "world", 
                FeedUrl = "https://www.thehindu.com/news/national/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1524492412937-b28074a5d7da?w=800",
                Region = "India",
                LocationScope = "National"
            },
            new() { 
                Name = "Times of India Top Stories", 
                CategorySlug = "world", 
                FeedUrl = "https://timesofindia.indiatimes.com/rssfeedstopstories.cms", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1532375810709-75b1da00537c?w=800",
                Region = "India",
                LocationScope = "National"
            },

            // 3. India - State & City / District / Local Feeds
            new() { 
                Name = "The Hindu Delhi", 
                CategorySlug = "world", 
                FeedUrl = "https://www.thehindu.com/news/cities/delhi/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1587474260584-136574528ed5?w=800",
                Region = "Delhi",
                LocationScope = "City/District"
            },
            new() { 
                Name = "The Hindu Mumbai", 
                CategorySlug = "business", 
                FeedUrl = "https://www.thehindu.com/news/cities/mumbai/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1570168007204-dfb528c6958f?w=800",
                Region = "Mumbai",
                LocationScope = "City/District"
            },
            new() { 
                Name = "The Hindu Bengaluru", 
                CategorySlug = "technology", 
                FeedUrl = "https://www.thehindu.com/news/cities/bangalore/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1596176530529-78163a4f7af2?w=800",
                Region = "Bengaluru",
                LocationScope = "City/District"
            },
            new() { 
                Name = "The Hindu Chennai", 
                CategorySlug = "world", 
                FeedUrl = "https://www.thehindu.com/news/cities/chennai/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1582510003544-4d00b7f74220?w=800",
                Region = "Chennai",
                LocationScope = "City/District"
            },
            new() { 
                Name = "The Hindu Hyderabad", 
                CategorySlug = "technology", 
                FeedUrl = "https://www.thehindu.com/news/cities/Hyderabad/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1605649487212-47bdab064df7?w=800",
                Region = "Hyderabad",
                LocationScope = "City/District"
            },
            new() { 
                Name = "The Hindu Kolkata", 
                CategorySlug = "world", 
                FeedUrl = "https://www.thehindu.com/news/cities/kolkata/feeder/default.rss", 
                DefaultImageUrl = "https://images.unsplash.com/photo-1558431382-27e303142255?w=800",
                Region = "Kolkata",
                LocationScope = "City/District"
            }
        };

        public LiveNewsService(
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<LiveNewsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public IReadOnlyList<LiveNewsFeedSource> GetConfiguredFeedSources() => DefaultFeeds;

        // Sync all default Worldwide, National, and City feeds
        public async Task<LiveNewsSyncResult> SyncAllFeedsAsync()
        {
            var result = new LiveNewsSyncResult();
            foreach (var feed in DefaultFeeds)
            {
                var feedResult = await SyncFeedAsync(feed.FeedUrl, feed.CategorySlug, feed.Name, feed.Region, feed.LocationScope);
                result.TotalArticlesFetched += feedResult.TotalArticlesFetched;
                result.NewArticlesAdded += feedResult.NewArticlesAdded;
                result.DuplicatesSkipped += feedResult.DuplicatesSkipped;
                result.ImportedTitles.AddRange(feedResult.ImportedTitles);
                result.Errors.AddRange(feedResult.Errors);
                await Task.Delay(400);
            }
            return result;
        }

        // Sync India National feeds
        public async Task<LiveNewsSyncResult> SyncIndianNationalAsync()
        {
            var result = new LiveNewsSyncResult();
            var nationalFeeds = DefaultFeeds.Where(f => f.LocationScope == "National");
            foreach (var feed in nationalFeeds)
            {
                var feedResult = await SyncFeedAsync(feed.FeedUrl, feed.CategorySlug, feed.Name, feed.Region, feed.LocationScope);
                result.TotalArticlesFetched += feedResult.TotalArticlesFetched;
                result.NewArticlesAdded += feedResult.NewArticlesAdded;
                result.DuplicatesSkipped += feedResult.DuplicatesSkipped;
                result.ImportedTitles.AddRange(feedResult.ImportedTitles);
                result.Errors.AddRange(feedResult.Errors);
                await Task.Delay(400);
            }
            return result;
        }

        // Sync City / District local feeds (e.g., Delhi, Mumbai, Bengaluru, etc.)
        public async Task<LiveNewsSyncResult> SyncCityFeedsAsync(string? city = null)
        {
            var result = new LiveNewsSyncResult();
            var cityFeeds = DefaultFeeds.Where(f => f.LocationScope == "City/District");
            if (!string.IsNullOrWhiteSpace(city))
            {
                cityFeeds = cityFeeds.Where(f => f.Region.Equals(city, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var feed in cityFeeds)
            {
                var feedResult = await SyncFeedAsync(feed.FeedUrl, feed.CategorySlug, feed.Name, feed.Region, feed.LocationScope);
                result.TotalArticlesFetched += feedResult.TotalArticlesFetched;
                result.NewArticlesAdded += feedResult.NewArticlesAdded;
                result.DuplicatesSkipped += feedResult.DuplicatesSkipped;
                result.ImportedTitles.AddRange(feedResult.ImportedTitles);
                result.Errors.AddRange(feedResult.Errors);
                await Task.Delay(400);
            }
            return result;
        }

        // Sync a single feed with Region and LocationScope tagging
        public async Task<LiveNewsSyncResult> SyncFeedAsync(
            string feedUrl, 
            string categorySlug, 
            string sourceName, 
            string region = "Worldwide", 
            string locationScope = "Global")
        {
            var result = new LiveNewsSyncResult();
            try
            {
                var rawItems = await FetchFeedItemsAsync(feedUrl);
                if (rawItems.Count == 0) return result;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get category and author
                var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == categorySlug) 
                               ?? await context.Categories.FirstOrDefaultAsync();
                var author = await context.Authors.FirstOrDefaultAsync();

                if (category == null || author == null) return result;

                foreach (var item in rawItems.Take(12))
                {
                    result.TotalArticlesFetched++;

                    if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length < 5) continue;

                    // Clean and truncate summary
                    string cleanSummary = Regex.Replace(item.Summary ?? "", "<.*?>", string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(cleanSummary)) cleanSummary = item.Title;
                    if (cleanSummary.Length > 280) cleanSummary = cleanSummary[..280] + "...";

                    string cleanContent = Regex.Replace(item.Content ?? "", "<.*?>", string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(cleanContent)) cleanContent = cleanSummary;

                    // Skip if title already in database
                    bool exists = await context.NewsArticles.AnyAsync(a => a.Title.ToLower() == item.Title.ToLower());
                    if (exists)
                    {
                        result.DuplicatesSkipped++;
                        continue;
                    }

                    string slug = SlugHelper.GenerateSlug(item.Title);
                    if (await context.NewsArticles.AnyAsync(a => a.Slug == slug))
                    {
                        slug = $"{slug}-{Guid.NewGuid().ToString("n")[..4]}";
                    }

                    string imageUrl = !string.IsNullOrWhiteSpace(item.ImageUrl) && item.ImageUrl.StartsWith("http") 
                        ? item.ImageUrl 
                        : GetFallbackImageForRegion(region);

                    var article = new NewsArticle
                    {
                        Title = item.Title,
                        Slug = slug,
                        Summary = cleanSummary,
                        Content = cleanContent,
                        ImageUrl = imageUrl,
                        CategoryId = category.Id,
                        AuthorId = author.Id,
                        IsPublished = true,
                        PublishedDate = item.PublishedDate ?? DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsLiveSynced = true,
                        SourceName = sourceName,
                        SourceUrl = item.Link,
                        Region = string.IsNullOrWhiteSpace(region) ? "Worldwide" : region,
                        LocationScope = string.IsNullOrWhiteSpace(locationScope) ? "Global" : locationScope
                    };

                    context.NewsArticles.Add(article);
                    result.NewArticlesAdded++;
                    result.ImportedTitles.Add(item.Title);
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error syncing feed {Feed}: {Msg}", sourceName, ex.Message);
                result.Errors.Add($"{sourceName}: {ex.Message}");
            }

            return result;
        }

        // Dual-resilient feed fetcher: tries rss2json API first, falls back to direct XML parsing
        private async Task<List<ParsedFeedItem>> FetchFeedItemsAsync(string feedUrl)
        {
            var items = new List<ParsedFeedItem>();
            var client = _httpClientFactory.CreateClient("LiveNewsClient");

            // 1. Try JSON REST API via rss2json
            try
            {
                string apiUrl = $"https://api.rss2json.com/v1/api.json?rss_url={Uri.EscapeDataString(feedUrl)}";
                var response = await client.GetFromJsonAsync<Rss2JsonResponse>(apiUrl);
                if (response != null && response.status == "ok" && response.items != null && response.items.Count > 0)
                {
                    foreach (var i in response.items)
                    {
                        DateTime? pub = null;
                        if (DateTime.TryParse(i.pubDate, out var pDate)) pub = pDate;

                        string? img = i.thumbnail;
                        if (string.IsNullOrWhiteSpace(img) && i.enclosure != null && !string.IsNullOrWhiteSpace(i.enclosure.link))
                        {
                            img = i.enclosure.link;
                        }

                        items.Add(new ParsedFeedItem
                        {
                            Title = i.title?.Trim() ?? "",
                            Link = i.link?.Trim() ?? "",
                            Summary = i.description?.Trim() ?? "",
                            Content = i.content?.Trim() ?? i.description?.Trim() ?? "",
                            ImageUrl = img,
                            PublishedDate = pub
                        });
                    }
                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("rss2json fetch skipped: {Msg}. Trying direct RSS XML...", ex.Message);
            }

            // 2. Direct XML RSS/Atom fallback
            try
            {
                var xmlString = await client.GetStringAsync(feedUrl);
                var doc = XDocument.Parse(xmlString);
                var xmlItems = doc.Descendants().Where(e => e.Name.LocalName == "item" || e.Name.LocalName == "entry").Take(15);

                foreach (var x in xmlItems)
                {
                    string title = x.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim() ?? "";
                    string link = x.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(link))
                    {
                        link = x.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value ?? "";
                    }

                    string summary = x.Elements().FirstOrDefault(e => e.Name.LocalName == "description" || e.Name.LocalName == "summary")?.Value?.Trim() ?? "";

                    DateTime? pub = null;
                    var pubString = x.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate" || e.Name.LocalName == "updated")?.Value;
                    if (DateTime.TryParse(pubString, out var pDate)) pub = pDate;

                    string? img = x.Elements().FirstOrDefault(e => e.Name.LocalName == "enclosure")?.Attribute("url")?.Value;

                    items.Add(new ParsedFeedItem
                    {
                        Title = title,
                        Link = link,
                        Summary = summary,
                        Content = summary,
                        ImageUrl = img,
                        PublishedDate = pub
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Direct XML parsing also failed for {Url}: {Msg}", feedUrl, ex.Message);
            }

            return items;
        }

        // Sync top stories from Hacker News Firebase REST API
        public async Task<LiveNewsSyncResult> SyncHackerNewsAsync()
        {
            var result = new LiveNewsSyncResult();
            try
            {
                var client = _httpClientFactory.CreateClient("LiveNewsClient");
                var topIds = await client.GetFromJsonAsync<List<int>>("https://hacker-news.firebaseio.com/v0/topstories.json");
                if (topIds == null) return result;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "technology") 
                               ?? await context.Categories.FirstOrDefaultAsync();
                var author = await context.Authors.FirstOrDefaultAsync();
                if (category == null || author == null) return result;

                foreach (var id in topIds.Take(10))
                {
                    result.TotalArticlesFetched++;
                    var story = await client.GetFromJsonAsync<HackerNewsItem>($"https://hacker-news.firebaseio.com/v0/item/{id}.json");
                    if (story == null || string.IsNullOrWhiteSpace(story.title)) continue;

                    bool exists = await context.NewsArticles.AnyAsync(a => a.Title.ToLower() == story.title.ToLower());
                    if (exists)
                    {
                        result.DuplicatesSkipped++;
                        continue;
                    }

                    string slug = SlugHelper.GenerateSlug(story.title);
                    if (await context.NewsArticles.AnyAsync(a => a.Slug == slug))
                    {
                        slug = $"{slug}-{Guid.NewGuid().ToString("n")[..4]}";
                    }

                    var article = new NewsArticle
                    {
                        Title = story.title,
                        Slug = slug,
                        Summary = $"Trending discussion on Hacker News by {story.by} with {story.score} points and {story.descendants} comments.",
                        Content = $"Full story and comments: {story.url ?? $"https://news.ycombinator.com/item?id={id}"}",
                        ImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=800",
                        CategoryId = category.Id,
                        AuthorId = author.Id,
                        IsPublished = true,
                        PublishedDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsLiveSynced = true,
                        SourceName = "Hacker News",
                        SourceUrl = story.url ?? $"https://news.ycombinator.com/item?id={id}",
                        Region = "Worldwide",
                        LocationScope = "Global"
                    };

                    context.NewsArticles.Add(article);
                    result.NewArticlesAdded++;
                    result.ImportedTitles.Add(story.title);
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Hacker News API error: {ex.Message}");
            }
            return result;
        }

        // Sync custom source configured in the database
        public async Task<LiveNewsSyncResult> SyncCustomSourceAsync(int sourceId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var source = await context.NewsFeedSources.FindAsync(sourceId);
            if (source == null) return new LiveNewsSyncResult { Errors = { "Feed source not found" } };

            var result = await SyncFeedAsync(source.FeedUrl, source.CategorySlug, source.Name, source.Region, source.LocationScope);
            source.LastSyncedAt = DateTime.UtcNow;
            source.TotalArticlesImported += result.NewArticlesAdded;
            await context.SaveChangesAsync();

            return result;
        }

        public Task<LiveNewsSyncResult> SyncFromNewsApiAsync(string? apiKey = null, string? category = null)
        {
            return Task.FromResult(new LiveNewsSyncResult { Errors = { "NewsAPI sync is optional; RSS & REST converter feeds provide live news without API keys." } });
        }

        public Task<LiveNewsSyncResult> SyncFromGNewsApiAsync(string? apiKey = null, string? category = null)
        {
            return Task.FromResult(new LiveNewsSyncResult { Errors = { "GNews API sync is optional; RSS & REST converter feeds provide live news without API keys." } });
        }

        private static string GetFallbackImageForRegion(string region)
        {
            return region switch
            {
                "India" => "https://images.unsplash.com/photo-1524492412937-b28074a5d7da?w=800",
                "Delhi" => "https://images.unsplash.com/photo-1587474260584-136574528ed5?w=800",
                "Mumbai" => "https://images.unsplash.com/photo-1570168007204-dfb528c6958f?w=800",
                "Bengaluru" => "https://images.unsplash.com/photo-1596176530529-78163a4f7af2?w=800",
                "Chennai" => "https://images.unsplash.com/photo-1582510003544-4d00b7f74220?w=800",
                "Hyderabad" => "https://images.unsplash.com/photo-1605649487212-47bdab064df7?w=800",
                "Kolkata" => "https://images.unsplash.com/photo-1558431382-27e303142255?w=800",
                _ => "https://images.unsplash.com/photo-1504711434969-e33886168f5c?w=800"
            };
        }

        private class ParsedFeedItem
        {
            public string Title { get; set; } = string.Empty;
            public string Link { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public DateTime? PublishedDate { get; set; }
        }

        private class Rss2JsonResponse
        {
            [JsonPropertyName("status")]
            public string? status { get; set; }

            [JsonPropertyName("items")]
            public List<Rss2JsonItem>? items { get; set; }
        }

        private class Rss2JsonItem
        {
            [JsonPropertyName("title")]
            public string? title { get; set; }

            [JsonPropertyName("pubDate")]
            public string? pubDate { get; set; }

            [JsonPropertyName("link")]
            public string? link { get; set; }

            [JsonPropertyName("author")]
            public string? author { get; set; }

            [JsonPropertyName("thumbnail")]
            public string? thumbnail { get; set; }

            [JsonPropertyName("description")]
            public string? description { get; set; }

            [JsonPropertyName("content")]
            public string? content { get; set; }

            [JsonPropertyName("enclosure")]
            public Rss2JsonEnclosure? enclosure { get; set; }
        }

        private class Rss2JsonEnclosure
        {
            [JsonPropertyName("link")]
            public string? link { get; set; }
        }

        private class HackerNewsItem
        {
            public string? title { get; set; }
            public string? url { get; set; }
            public string? by { get; set; }
            public int score { get; set; }
            public int descendants { get; set; }
        }
    }
}
