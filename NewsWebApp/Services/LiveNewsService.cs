using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
    /// Simple and clean service that fetches live news from RSS feeds and APIs.
    /// Easy to understand for beginners: downloads XML, extracts headlines, and saves them to SQLite.
    /// </summary>
    public class LiveNewsService : ILiveNewsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LiveNewsService> _logger;

        // Default global RSS feeds
        private static readonly List<LiveNewsFeedSource> DefaultFeeds = new()
        {
            new() { Name = "BBC World", CategorySlug = "world", FeedUrl = "https://feeds.bbci.co.uk/news/world/rss.xml", DefaultImageUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=800" },
            new() { Name = "TechCrunch", CategorySlug = "technology", FeedUrl = "https://techcrunch.com/feed/", DefaultImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=800" },
            new() { Name = "BBC Business", CategorySlug = "business", FeedUrl = "https://feeds.bbci.co.uk/news/business/rss.xml", DefaultImageUrl = "https://images.unsplash.com/photo-1590283603385-17ffb3a7f29f?w=800" },
            new() { Name = "BBC Science", CategorySlug = "science", FeedUrl = "https://feeds.bbci.co.uk/news/science_and_environment/rss.xml", DefaultImageUrl = "https://images.unsplash.com/photo-1473341304170-971dccb5ac1e?w=800" },
            new() { Name = "BBC Sport", CategorySlug = "sports", FeedUrl = "https://feeds.bbci.co.uk/sport/rss.xml", DefaultImageUrl = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=800" }
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

        // Sync all default RSS feeds
        public async Task<LiveNewsSyncResult> SyncAllFeedsAsync()
        {
            var result = new LiveNewsSyncResult();
            foreach (var feed in DefaultFeeds)
            {
                var feedResult = await SyncFeedAsync(feed.FeedUrl, feed.CategorySlug, feed.Name);
                result.TotalArticlesFetched += feedResult.TotalArticlesFetched;
                result.NewArticlesAdded += feedResult.NewArticlesAdded;
                result.DuplicatesSkipped += feedResult.DuplicatesSkipped;
                result.ImportedTitles.AddRange(feedResult.ImportedTitles);
                result.Errors.AddRange(feedResult.Errors);
            }
            return result;
        }

        // Sync a single RSS/Atom feed
        public async Task<LiveNewsSyncResult> SyncFeedAsync(string feedUrl, string categorySlug, string sourceName)
        {
            var result = new LiveNewsSyncResult();
            try
            {
                var client = _httpClientFactory.CreateClient("LiveNewsClient");
                var xmlString = await client.GetStringAsync(feedUrl);
                var doc = XDocument.Parse(xmlString);

                // Find items (RSS <item> or Atom <entry>)
                var items = doc.Descendants().Where(e => e.Name.LocalName == "item" || e.Name.LocalName == "entry").Take(15);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Get category and default author
                var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == categorySlug) 
                               ?? await context.Categories.FirstOrDefaultAsync();
                var author = await context.Authors.FirstOrDefaultAsync();

                if (category == null || author == null) return result;

                foreach (var item in items)
                {
                    result.TotalArticlesFetched++;

                    string title = item.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim() ?? "";
                    string link = item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(link))
                    {
                        link = item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value ?? "";
                    }

                    string summary = item.Elements().FirstOrDefault(e => e.Name.LocalName == "description" || e.Name.LocalName == "summary")?.Value?.Trim() ?? "";
                    summary = Regex.Replace(summary, "<.*?>", string.Empty); // Clean HTML tags

                    if (string.IsNullOrWhiteSpace(title) || title.Length < 5) continue;

                    // Skip if already in database
                    bool exists = await context.NewsArticles.AnyAsync(a => a.Title.ToLower() == title.ToLower());
                    if (exists)
                    {
                        result.DuplicatesSkipped++;
                        continue;
                    }

                    string slug = SlugHelper.GenerateSlug(title);
                    if (await context.NewsArticles.AnyAsync(a => a.Slug == slug))
                    {
                        slug = $"{slug}-{Guid.NewGuid().ToString("n")[..4]}";
                    }

                    var article = new NewsArticle
                    {
                        Title = title,
                        Slug = slug,
                        Summary = summary.Length > 280 ? summary[..280] + "..." : (string.IsNullOrWhiteSpace(summary) ? title : summary),
                        Content = summary.Length > 0 ? summary : title,
                        ImageUrl = "https://images.unsplash.com/photo-1504711434969-e33886168f5c?w=800",
                        CategoryId = category.Id,
                        AuthorId = author.Id,
                        IsPublished = true,
                        PublishedDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsLiveSynced = true,
                        SourceName = sourceName,
                        SourceUrl = link
                    };

                    context.NewsArticles.Add(article);
                    result.NewArticlesAdded++;
                    result.ImportedTitles.Add(title);
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
                        SourceUrl = story.url ?? $"https://news.ycombinator.com/item?id={id}"
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

            var result = await SyncFeedAsync(source.FeedUrl, source.CategorySlug, source.Name);
            source.LastSyncedAt = DateTime.UtcNow;
            source.TotalArticlesImported += result.NewArticlesAdded;
            await context.SaveChangesAsync();

            return result;
        }

        public Task<LiveNewsSyncResult> SyncFromNewsApiAsync(string? apiKey = null, string? category = null)
        {
            return Task.FromResult(new LiveNewsSyncResult { Errors = { "NewsAPI sync is optional; RSS feeds provide live news without API keys." } });
        }

        public Task<LiveNewsSyncResult> SyncFromGNewsApiAsync(string? apiKey = null, string? category = null)
        {
            return Task.FromResult(new LiveNewsSyncResult { Errors = { "GNews API sync is optional; RSS feeds provide live news without API keys." } });
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
