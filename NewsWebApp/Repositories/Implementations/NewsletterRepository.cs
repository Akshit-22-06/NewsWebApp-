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
    public class NewsletterRepository : INewsletterRepository
    {
        private readonly ApplicationDbContext _context;

        public NewsletterRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> SubscribeAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Please provide a valid email address.");
            }

            var cleanEmail = email.Trim().ToLower();

            var existing = await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.Email.ToLower() == cleanEmail);

            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await _context.SaveChangesAsync();
                    return (true, "Welcome back! Your subscription has been reactivated.");
                }
                return (true, "You are already subscribed to our Morning Dispatch.");
            }

            var subscriber = new NewsletterSubscriber
            {
                Email = cleanEmail,
                SubscribedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _context.NewsletterSubscribers.AddAsync(subscriber);
            await _context.SaveChangesAsync();

            return (true, "Thank you for subscribing! You will receive our morning breaking news briefing.");
        }

        public async Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync()
        {
            return await _context.NewsletterSubscribers
                .AsNoTracking()
                .OrderByDescending(s => s.SubscribedAt)
                .ToListAsync();
        }

        public async Task<int> GetSubscriberCountAsync()
        {
            return await _context.NewsletterSubscribers
                .CountAsync(s => s.IsActive);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sub = await _context.NewsletterSubscribers.FindAsync(id);
            if (sub != null)
            {
                _context.NewsletterSubscribers.Remove(sub);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}
