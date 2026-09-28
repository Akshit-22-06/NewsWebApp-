using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for displaying full news article details, comments, and related articles.
    /// </summary>
    public class NewsDetailsViewModel
    {
        public NewsArticle Article { get; set; } = null!;
        public IEnumerable<NewsArticle> RelatedArticles { get; set; } = new List<NewsArticle>();
        public AddCommentViewModel NewComment { get; set; } = new AddCommentViewModel();
        public bool IsBookmarked { get; set; } = false;
        public int EstimatedReadMinutes { get; set; } = 3;
    }
}
