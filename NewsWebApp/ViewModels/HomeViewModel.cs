using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the public Home page displaying Currents API live news and local NewsBase content.
    /// </summary>
    public class HomeViewModel
    {
        public IEnumerable<NewsDto> LiveNews { get; set; } = new List<NewsDto>();
        public IEnumerable<NewsArticle> FeaturedArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> LatestArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> PopularArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<TrendingArticleDto> TrendingArticles { get; set; } = new List<TrendingArticleDto>();
        public IEnumerable<(Category Category, int ArticleCount)> CategoriesWithCounts { get; set; } = new List<(Category, int)>();
    }

    /// <summary>
    /// Represents a trending article ranked by recent (24h) engagement and recency.
    /// </summary>
    public class TrendingArticleDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "General";
        public string CategorySlug { get; set; } = "general";
        public string SourceName { get; set; } = string.Empty;
        public int ViewsToday { get; set; }
        public int TotalViews { get; set; }
        public int LikeCount { get; set; }
        public System.DateTime? PublishedDate { get; set; }
        public string FormattedTimeAgo { get; set; } = string.Empty;
        public string TrendingReason { get; set; } = string.Empty;
    }
}
