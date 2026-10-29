using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Author Dashboard, showing their articles and approval workflow status counts.
    /// </summary>
    public class AuthorDashboardViewModel
    {
        public Author Author { get; set; } = null!;
        public IEnumerable<NewsArticle> Articles { get; set; } = new List<NewsArticle>();
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}
