using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Admin control center summarizing content statistics and recent activity.
    /// </summary>
    public class AdminDashboardViewModel
    {
        public int TotalArticles { get; set; }
        public int PublishedArticles { get; set; }
        public int DraftArticles { get; set; }
        public int TotalCategories { get; set; }
        public int TotalComments { get; set; }
        public int TotalUsers { get; set; }
        public int TotalViews { get; set; }
        public int TotalSubscribers { get; set; }
        public int TotalLiveFeeds { get; set; }

        public IEnumerable<NewsArticle> RecentArticles { get; set; } = new List<NewsArticle>();
        public IEnumerable<Comment> RecentComments { get; set; } = new List<Comment>();
    }
}
