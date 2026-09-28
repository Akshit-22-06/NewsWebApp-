using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    public class BookmarkListViewModel
    {
        public IEnumerable<NewsArticle> BookmarkedArticles { get; set; } = new List<NewsArticle>();
        public int TotalBookmarks { get; set; }
    }
}
