using System;
using System.Collections.Generic;
using NewsWebApp.Models;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for browsing and searching news articles with pagination, sorting, and category filters.
    /// </summary>
    public class NewsListViewModel
    {
        public IEnumerable<NewsArticle> Articles { get; set; } = new List<NewsArticle>();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 6;
        public int TotalArticles { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalArticles / PageSize));
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public string? SortBy { get; set; }
        public string? SourceType { get; set; }
        public string? TimeRange { get; set; }
        public string? Region { get; set; }
        public string? LocationScope { get; set; }
        public IEnumerable<string> AvailableRegions { get; set; } = new List<string>();

        public IEnumerable<Category> Categories { get; set; } = new List<Category>();
    }
}
