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
    /// Entity Framework Core implementation of ICategoryRepository.
    /// Handles database operations for news categories.
    /// </summary>
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories
                .Include(c => c.NewsArticles.Where(a => a.IsPublished))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            return await _context.Categories
                .Include(c => c.NewsArticles.Where(a => a.IsPublished).OrderByDescending(a => a.PublishedDate))
                .FirstOrDefaultAsync(c => c.Slug.ToLower() == slug.ToLower());
        }

        public async Task AddAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
        {
            var query = _context.Categories.AsNoTracking().Where(c => c.Slug.ToLower() == slug.ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await _context.Categories.CountAsync();
        }

        public async Task<IEnumerable<(Category Category, int ArticleCount)>> GetCategoriesWithCountAsync()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Select(c => new
                {
                    Category = c,
                    ArticleCount = c.NewsArticles.Count(a => a.IsPublished)
                })
                .OrderBy(x => x.Category.Name)
                .ToListAsync();

            return categories.Select(x => (x.Category, x.ArticleCount));
        }
    }
}
