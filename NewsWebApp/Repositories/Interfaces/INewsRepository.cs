using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    /// <summary>
    /// Simplified, beginner-friendly repository interface for news articles.
    /// </summary>
    public interface INewsRepository
    {
        Task<IEnumerable<NewsArticle>> GetAllAsync();
        Task<NewsArticle?> GetByIdAsync(int id);
        Task<NewsArticle?> GetBySlugAsync(string slug);
        Task<IEnumerable<NewsArticle>> GetLatestAsync(int count);
        Task<IEnumerable<NewsArticle>> GetFeaturedAsync(int count);
        Task<IEnumerable<NewsArticle>> GetPopularAsync(int count);
        Task<IEnumerable<NewsArticle>> GetMostDiscussedAsync(int count);
        Task<IEnumerable<NewsArticle>> GetLiveBreakingNewsAsync(int count);
        Task<IEnumerable<NewsArticle>> GetRelatedAsync(int articleId, int categoryId, int count);
        
        Task<(IEnumerable<NewsArticle> Articles, int TotalCount)> GetPagedPublishedAsync(
            string? search, int? categoryId, string? sortBy, int page, int pageSize, string? sourceType = null, string? timeRange = null);
        
        Task<(IEnumerable<NewsArticle> Articles, int TotalCount)> GetPagedAdminAsync(
            string? search, int? categoryId, bool? isPublished, int page, int pageSize, string? authorUserId = null);

        Task AddAsync(NewsArticle article);
        Task UpdateAsync(NewsArticle article);
        Task DeleteAsync(int id);
        
        Task IncrementViewCountAsync(int id);
        Task IncrementLikeCountAsync(int id);
        Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
        Task<bool> TitleExistsAsync(string title);
        Task<bool> SourceUrlExistsAsync(string sourceUrl);

        Task<int> GetTotalCountAsync();
        Task<int> GetPublishedCountAsync();
        Task<int> GetDraftCountAsync();
        Task<int> GetTotalViewsAsync();
        Task<int> GetLiveSyncedCountAsync();
    }
}
