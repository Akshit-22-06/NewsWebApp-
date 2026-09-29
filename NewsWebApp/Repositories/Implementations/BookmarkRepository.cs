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
    public class BookmarkRepository : IBookmarkRepository
    {
        private readonly ApplicationDbContext _context;

        public BookmarkRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsBookmarkedAsync(string userId, int articleId)
        {
            if (string.IsNullOrEmpty(userId)) return false;
            return await _context.ArticleBookmarks
                .AsNoTracking()
                .AnyAsync(b => b.UserId == userId && b.NewsArticleId == articleId);
        }

        public async Task<bool> ToggleBookmarkAsync(string userId, int articleId)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            var existing = await _context.ArticleBookmarks
                .FirstOrDefaultAsync(b => b.UserId == userId && b.NewsArticleId == articleId);

            if (existing != null)
            {
                _context.ArticleBookmarks.Remove(existing);
                await _context.SaveChangesAsync();
                return false; // Removed
            }
            else
            {
                var bookmark = new ArticleBookmark
                {
                    UserId = userId,
                    NewsArticleId = articleId,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.ArticleBookmarks.AddAsync(bookmark);
                await _context.SaveChangesAsync();
                return true; // Added
            }
        }

        public async Task<IEnumerable<NewsArticle>> GetUserBookmarksAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return Enumerable.Empty<NewsArticle>();

            return await _context.ArticleBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .Include(b => b.NewsArticle).ThenInclude(a => a!.Category)
                .Include(b => b.NewsArticle).ThenInclude(a => a!.Author)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => b.NewsArticle!)
                .Where(a => a != null && a.IsPublished)
                .ToListAsync();
        }

        public async Task<int> GetUserBookmarkCountAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return 0;
            return await _context.ArticleBookmarks
                .CountAsync(b => b.UserId == userId);
        }
    }
}
