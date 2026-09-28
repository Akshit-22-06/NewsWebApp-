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
    /// Entity Framework Core implementation of IAuthorRepository.
    /// Handles database operations for author profiles.
    /// </summary>
    public class AuthorRepository : IAuthorRepository
    {
        private readonly ApplicationDbContext _context;

        public AuthorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Author>> GetAllAsync()
        {
            return await _context.Authors
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.NewsArticles)
                .OrderBy(a => a.DisplayName)
                .ToListAsync();
        }

        public async Task<Author?> GetByIdAsync(int id)
        {
            return await _context.Authors
                .Include(a => a.User)
                .Include(a => a.NewsArticles.Where(n => n.IsPublished).OrderByDescending(n => n.PublishedDate))
                    .ThenInclude(n => n.Category)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Author?> GetByUserIdAsync(string userId)
        {
            return await _context.Authors
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        public async Task AddAsync(Author author)
        {
            await _context.Authors.AddAsync(author);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Author author)
        {
            _context.Authors.Update(author);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author != null)
            {
                _context.Authors.Remove(author);
                await _context.SaveChangesAsync();
            }
        }
    }
}
