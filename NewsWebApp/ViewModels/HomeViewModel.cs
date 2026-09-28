using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the public Home page displaying curated news sections.
    /// </summary>
    public class HomeViewModel
    {
        public IEnumerable<NewsArticle> FeaturedArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> LatestArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> PopularArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> BreakingNewsTicker { get; set; } = new List<NewsArticle>();
        public IEnumerable<NewsArticle> MostDiscussedArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<(Category Category, int ArticleCount)> CategoriesWithCounts { get; set; } = new List<(Category, int)>();
    }
}
