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
    public class NewsFeedSourceRepository : INewsFeedSourceRepository
    {
        private readonly ApplicationDbContext _context;

        public NewsFeedSourceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<NewsFeedSource>> GetAllAsync()
        {
            return await _context.NewsFeedSources
                .AsNoTracking()
                .OrderBy(s => s.CategorySlug)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<NewsFeedSource>> GetActiveAsync()
        {
            return await _context.NewsFeedSources
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<NewsFeedSource?> GetByIdAsync(int id)
        {
            return await _context.NewsFeedSources.FindAsync(id);
        }

        public async Task<NewsFeedSource?> GetByUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var cleanUrl = url.Trim().ToLower();
            return await _context.NewsFeedSources
                .FirstOrDefaultAsync(s => s.FeedUrl.ToLower() == cleanUrl);
        }

        public async Task AddAsync(NewsFeedSource source)
        {
            await _context.NewsFeedSources.AddAsync(source);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(NewsFeedSource source)
        {
            _context.NewsFeedSources.Update(source);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var source = await _context.NewsFeedSources.FindAsync(id);
            if (source != null)
            {
                _context.NewsFeedSources.Remove(source);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ToggleActiveAsync(int id)
        {
            var source = await _context.NewsFeedSources.FindAsync(id);
            if (source != null)
            {
                source.IsActive = !source.IsActive;
                await _context.SaveChangesAsync();
                return source.IsActive;
            }
            return false;
        }

        public async Task RecordSyncAsync(int id, int articlesImported)
        {
            var source = await _context.NewsFeedSources.FindAsync(id);
            if (source != null)
            {
                source.LastSyncedAt = DateTime.UtcNow;
                source.TotalArticlesImported += articlesImported;
                await _context.SaveChangesAsync();
            }
        }
    }
}
