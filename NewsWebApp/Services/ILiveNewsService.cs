using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewsWebApp.Services
{
    public class LiveNewsFeedSource
    {
        public string Name { get; set; } = string.Empty;
        public string CategorySlug { get; set; } = string.Empty;
        public string FeedUrl { get; set; } = string.Empty;
        public string DefaultImageUrl { get; set; } = string.Empty;
    }

    public class LiveNewsSyncResult
    {
        public int TotalArticlesFetched { get; set; }
        public int NewArticlesAdded { get; set; }
        public int DuplicatesSkipped { get; set; }
        public List<string> ImportedTitles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service for fetching, parsing, normalizing, and ingesting live breaking news from external APIs and RSS feeds.
    /// Supports Hacker News API, GNews API, NewsAPI.org, and custom RSS/Atom feeds.
    /// </summary>
    public interface ILiveNewsService
    {
        Task<LiveNewsSyncResult> SyncAllFeedsAsync();
        Task<LiveNewsSyncResult> SyncFeedAsync(string feedUrl, string categorySlug, string sourceName);
        Task<LiveNewsSyncResult> SyncHackerNewsAsync();
        Task<LiveNewsSyncResult> SyncFromNewsApiAsync(string? apiKey = null, string? category = null);
        Task<LiveNewsSyncResult> SyncFromGNewsApiAsync(string? apiKey = null, string? category = null);
        Task<LiveNewsSyncResult> SyncCustomSourceAsync(int sourceId);
        IReadOnlyList<LiveNewsFeedSource> GetConfiguredFeedSources();
    }
}
