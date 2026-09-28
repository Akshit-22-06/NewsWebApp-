using System.Collections.Generic;
using NewsWebApp.Models;
using NewsWebApp.Services;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Live News Control Center in the Admin panel.
    /// Supports multi-feed status, dynamic DB feeds, Hacker News, GNews, and NewsAPI.
    /// </summary>
    public class LiveNewsHubViewModel
    {
        public IReadOnlyList<LiveNewsFeedSource> FeedSources { get; set; } = new List<LiveNewsFeedSource>();
        public IEnumerable<NewsFeedSource> CustomFeedSources { get; set; } = new List<NewsFeedSource>();
        public int TotalLiveArticles { get; set; }
        public int TotalArticles { get; set; }
        public bool IsAutoSyncEnabled { get; set; }
        public int SyncIntervalMinutes { get; set; }
        public bool HasNewsApiKey { get; set; }
        public bool HasGNewsApiKey { get; set; }
        public string? NewsApiKeyMasked { get; set; }
        public string? GNewsApiKeyMasked { get; set; }
        public LiveNewsSyncResult? LastSyncResult { get; set; }
        public IEnumerable<NewsArticle> RecentLiveArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<Category> AvailableCategories { get; set; } = new List<Category>();
    }
}
