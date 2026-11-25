using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for the public News index page, uniting NewsBase local editorial articles
    /// with the Currents API global live wire.
    /// </summary>
    public class NewsIndexViewModel
    {
        public IEnumerable<NewsArticle> LocalArticles { get; set; } = new List<NewsArticle>();
        public List<NewsDto> LiveNews { get; set; } = new();
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        public string? SearchTerm { get; set; }
        public string? SelectedCategory { get; set; }
        public string? SelectedCountry { get; set; }
        public string? SelectedLanguage { get; set; }
        public string SourceType { get; set; } = "all"; // "all", "editorial", "wire"

        public string? ErrorMessage { get; set; }
    }
}
