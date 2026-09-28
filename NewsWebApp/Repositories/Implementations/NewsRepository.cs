using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NewsWebApp.Data;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;

namespace NewsWebApp.Repositories.Implementations
{
    /// <summary>
    /// Clean, beginner-friendly EF Core implementation of INewsRepository.
    /// Uses standard LINQ queries to interact with the database.
    /// </summary>
    public class NewsRepository : INewsRepository
    {
        private readonly ApplicationDbContext _context;

        public NewsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<NewsArticle>> GetAllAsync()
        {
            return await _context.NewsArticles
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<NewsArticle?> GetByIdAsync(int id)
        {
            return await _context.NewsArticles
                .Include(a => a.Category)
                .Include(a => a.Author).ThenInclude(au => au!.User)
                .Include(a => a.Comments.OrderByDescending(c => c.CreatedAt)).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<NewsArticle?> GetBySlugAsync(string slug)
        {
            return await _context.NewsArticles
                .Include(a => a.Category)
                .Include(a => a.Author).ThenInclude(au => au!.User)
                .Include(a => a.Comments.OrderByDescending(c => c.CreatedAt)).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(a => a.Slug.ToLower() == slug.ToLower());
        }

        public async Task<IEnumerable<NewsArticle>> GetLatestAsync(int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished)
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsArticle>> GetFeaturedAsync(int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished && a.IsFeatured)
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsArticle>> GetPopularAsync(int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished)
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.ViewCount)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsArticle>> GetMostDiscussedAsync(int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished)
                .Include(a => a.Category)
                .Include(a => a.Author)
                .OrderByDescending(a => a.Comments.Count)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsArticle>> GetLiveBreakingNewsAsync(int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished && a.IsLiveSynced)
                .Include(a => a.Category)
                .OrderByDescending(a => a.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsArticle>> GetRelatedAsync(int articleId, int categoryId, int count)
        {
            return await _context.NewsArticles
                .Where(a => a.IsPublished && a.CategoryId == categoryId && a.Id != articleId)
                .Include(a => a.Category)
                .OrderByDescending(a => a.PublishedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<(IEnumerable<NewsArticle> Articles, int TotalCount)> GetPagedPublishedAsync(
            string? search, int? categoryId, string? sortBy, int page, int pageSize, string? sourceType = null, string? timeRange = null)
        {
            var query = _context.NewsArticles
                .Where(a => a.IsPublished)
                .Include(a => a.Category)
                .Include(a => a.Author)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower().Trim();
                query = query.Where(a => a.Title.ToLower().Contains(term) || a.Summary.ToLower().Contains(term));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.CategoryId == categoryId.Value);
            }

            if (sourceType == "live") query = query.Where(a => a.IsLiveSynced);
            if (sourceType == "editorial") query = query.Where(a => !a.IsLiveSynced);

            query = sortBy switch
            {
                "popular" => query.OrderByDescending(a => a.ViewCount),
                "discussed" => query.OrderByDescending(a => a.Comments.Count),
                "oldest" => query.OrderBy(a => a.PublishedDate),
                _ => query.OrderByDescending(a => a.PublishedDate)
            };

            int totalCount = await query.CountAsync();
            var articles = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (articles, totalCount);
        }

        public async Task<(IEnumerable<NewsArticle> Articles, int TotalCount)> GetPagedAdminAsync(
            string? search, int? categoryId, bool? isPublished, int page, int pageSize, string? authorUserId = null)
        {
            var query = _context.NewsArticles
                .Include(a => a.Category)
                .Include(a => a.Author)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower().Trim();
                query = query.Where(a => a.Title.ToLower().Contains(term));
            }

            if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);
            if (isPublished.HasValue) query = query.Where(a => a.IsPublished == isPublished.Value);
            if (!string.IsNullOrEmpty(authorUserId)) query = query.Where(a => a.Author != null && a.Author.UserId == authorUserId);

            query = query.OrderByDescending(a => a.CreatedAt);

            int totalCount = await query.CountAsync();
            var articles = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (articles, totalCount);
        }

        public async Task AddAsync(NewsArticle article)
        {
            await _context.NewsArticles.AddAsync(article);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(NewsArticle article)
        {
            _context.NewsArticles.Update(article);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var article = await _context.NewsArticles.FindAsync(id);
            if (article != null)
            {
                _context.NewsArticles.Remove(article);
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncrementViewCountAsync(int id)
        {
            var article = await _context.NewsArticles.FindAsync(id);
            if (article != null)
            {
                article.ViewCount++;
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncrementLikeCountAsync(int id)
        {
            var article = await _context.NewsArticles.FindAsync(id);
            if (article != null)
            {
                article.LikeCount++;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
        {
            var query = _context.NewsArticles.Where(a => a.Slug.ToLower() == slug.ToLower());
            if (excludeId.HasValue) query = query.Where(a => a.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> TitleExistsAsync(string title)
        {
            string clean = title.Trim().ToLower();
            return await _context.NewsArticles.AnyAsync(a => a.Title.ToLower() == clean);
        }

        public async Task<bool> SourceUrlExistsAsync(string sourceUrl)
        {
            return await _context.NewsArticles.AnyAsync(a => a.SourceUrl == sourceUrl);
        }

        public async Task<int> GetTotalCountAsync() => await _context.NewsArticles.CountAsync();
        public async Task<int> GetPublishedCountAsync() => await _context.NewsArticles.CountAsync(a => a.IsPublished);
        public async Task<int> GetDraftCountAsync() => await _context.NewsArticles.CountAsync(a => !a.IsPublished);
        public async Task<int> GetTotalViewsAsync() => await _context.NewsArticles.SumAsync(a => a.ViewCount);
        public async Task<int> GetLiveSyncedCountAsync() => await _context.NewsArticles.CountAsync(a => a.IsLiveSynced);
    }
}
