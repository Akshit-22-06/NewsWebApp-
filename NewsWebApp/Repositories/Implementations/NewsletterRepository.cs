using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        private static readonly EmailAddressAttribute EmailValidator = new();

        public NewsletterRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. SUBSCRIBER MANAGEMENT
        // ==========================================

        public async Task<(bool Success, string Message, bool IsNewOrReactivated, NewsletterSubscriber? Subscriber)> SubscribeAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Please enter a valid email address.", false, null);
            }

            var cleanEmail = email.Trim().ToLowerInvariant();

            if (!EmailValidator.IsValid(cleanEmail))
            {
                return (false, "Please enter a valid email address.", false, null);
            }

            var existing = await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.Email.ToLower() == cleanEmail);

            if (existing != null)
            {
                if (existing.IsActive)
                {
                    return (false, "This email address is already subscribed to our newsletter.", false, existing);
                }

                // Reactivate previously unsubscribed reader
                existing.IsActive = true;
                existing.UnsubscribedAt = null;
                if (string.IsNullOrWhiteSpace(existing.UnsubscribeToken))
                {
                    existing.UnsubscribeToken = Guid.NewGuid().ToString("N");
                }

                await _context.SaveChangesAsync();
                return (true, "Thank you for subscribing! You are now part of The Morning Dispatch.", true, existing);
            }

            var subscriber = new NewsletterSubscriber
            {
                Email = cleanEmail,
                SubscribedAt = DateTime.UtcNow,
                IsActive = true,
                UnsubscribeToken = Guid.NewGuid().ToString("N")
            };

            await _context.NewsletterSubscribers.AddAsync(subscriber);
            await _context.SaveChangesAsync();

            return (true, "Thank you for subscribing! You are now part of The Morning Dispatch.", true, subscriber);
        }

        public async Task<(bool Success, string Message, NewsletterSubscriber? Subscriber)> UnsubscribeByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Invalid or missing unsubscribe token.", null);
            }

            var cleanToken = token.Trim();
            var subscriber = await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.UnsubscribeToken == cleanToken);

            if (subscriber == null)
            {
                return (false, "We could not find an active subscription matching this link.", null);
            }

            if (!subscriber.IsActive)
            {
                return (true, "You are already unsubscribed from The Morning Dispatch.", subscriber);
            }

            subscriber.IsActive = false;
            subscriber.UnsubscribedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "You have been successfully unsubscribed from The Morning Dispatch.", subscriber);
        }

        public async Task<NewsletterSubscriber?> GetByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.UnsubscribeToken == token.Trim());
        }

        public async Task<NewsletterSubscriber?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var clean = email.Trim().ToLowerInvariant();
            return await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.Email.ToLower() == clean);
        }

        public async Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync(string? search = null)
        {
            var query = _context.NewsletterSubscribers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(s => s.Email.ToLower().Contains(term));
            }

            return await query
                .OrderByDescending(s => s.SubscribedAt)
                .ToListAsync();
        }

        public async Task<List<NewsletterSubscriber>> GetActiveSubscribersAsync()
        {
            return await _context.NewsletterSubscribers
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        public async Task<int> GetActiveSubscriberCountAsync()
        {
            return await _context.NewsletterSubscribers.CountAsync(s => s.IsActive);
        }

        public Task<int> GetSubscriberCountAsync() => GetActiveSubscriberCountAsync();

        public async Task<int> GetUnsubscribedCountAsync()
        {
            return await _context.NewsletterSubscribers.CountAsync(s => !s.IsActive);
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

        public async Task<bool> ToggleStatusAsync(int id)
        {
            var sub = await _context.NewsletterSubscribers.FindAsync(id);
            if (sub != null)
            {
                sub.IsActive = !sub.IsActive;
                if (!sub.IsActive)
                {
                    sub.UnsubscribedAt = DateTime.UtcNow;
                }
                else
                {
                    sub.UnsubscribedAt = null;
                }
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        // ==========================================
        // 2. EDITION MANAGEMENT
        // ==========================================

        public async Task<NewsletterEdition?> GetEditionByDateAsync(string editionDate)
        {
            return await _context.NewsletterEditions
                .FirstOrDefaultAsync(e => e.EditionDate == editionDate);
        }

        public async Task<NewsletterEdition?> GetLatestEditionAsync()
        {
            return await _context.NewsletterEditions
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<NewsletterEdition?> GetEditionByIdAsync(int id)
        {
            return await _context.NewsletterEditions.FindAsync(id);
        }

        public async Task<List<NewsletterEdition>> GetAllEditionsAsync(int take = 30)
        {
            return await _context.NewsletterEditions
                .AsNoTracking()
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<NewsletterEdition> SaveEditionAsync(NewsletterEdition edition)
        {
            if (edition.Id == 0)
            {
                await _context.NewsletterEditions.AddAsync(edition);
            }
            else
            {
                _context.NewsletterEditions.Update(edition);
            }

            await _context.SaveChangesAsync();
            return edition;
        }

        // ==========================================
        // 3. SETTINGS & SCHEDULING CONFIGURATION
        // ==========================================

        public async Task<NewsletterSetting> GetSettingsAsync()
        {
            var setting = await _context.NewsletterSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new NewsletterSetting
                {
                    IsAutoSendEnabled = true,
                    SendHour = 7,
                    SendMinute = 0,
                    TimeZoneId = "Asia/Kolkata",
                    SenderName = "The Morning Dispatch | NewsBase",
                    SenderEmail = "newsletter@newsbase.org",
                    DailyEmailQuota = 300,
                    EmailsSentToday = 0,
                    QuotaResetDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd")
                };
                await _context.NewsletterSettings.AddAsync(setting);
                await _context.SaveChangesAsync();
            }
            else if (setting.DailyEmailQuota <= 0)
            {
                setting.DailyEmailQuota = 300;
                await _context.SaveChangesAsync();
            }
            return setting;
        }

        public async Task<NewsletterSetting> UpdateSettingsAsync(NewsletterSetting settings)
        {
            var existing = await _context.NewsletterSettings.FirstOrDefaultAsync();
            if (existing == null)
            {
                await _context.NewsletterSettings.AddAsync(settings);
            }
            else
            {
                existing.IsAutoSendEnabled = settings.IsAutoSendEnabled;
                existing.SendHour = settings.SendHour;
                existing.SendMinute = settings.SendMinute;
                existing.TimeZoneId = settings.TimeZoneId;
                existing.SenderName = settings.SenderName;
                existing.SenderEmail = settings.SenderEmail;
                existing.AdminTestEmail = settings.AdminTestEmail;
                existing.LastRunAt = settings.LastRunAt;
                existing.NextRunAt = settings.NextRunAt;
                existing.LastRunStatus = settings.LastRunStatus;
                existing.DailyEmailQuota = settings.DailyEmailQuota > 0 ? settings.DailyEmailQuota : 300;
                existing.EmailsSentToday = settings.EmailsSentToday;
                existing.QuotaResetDateUtc = settings.QuotaResetDateUtc;
            }

            await _context.SaveChangesAsync();
            return settings;
        }

        // ==========================================
        // 4. BREVO DAILY QUOTA & DELIVERY TRACKING
        // ==========================================

        public async Task<List<NewsletterSubscriber>> GetPendingSubscribersForEditionAsync(int editionId)
        {
            return await _context.NewsletterSubscribers
                .Where(s => s.IsActive && s.LastDeliveredEditionId != editionId)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        public async Task MarkSubscribersDeliveredAsync(IEnumerable<int> subscriberIds, int editionId)
        {
            var idList = subscriberIds.ToList();
            if (idList.Count == 0) return;

            var subscribers = await _context.NewsletterSubscribers
                .Where(s => idList.Contains(s.Id))
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var s in subscribers)
            {
                s.LastDeliveredEditionId = editionId;
                s.LastDeliveredAt = now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task RecordEmailsSentAsync(int count)
        {
            if (count <= 0) return;

            var settings = await GetSettingsAsync();
            var todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (settings.QuotaResetDateUtc != todayUtc)
            {
                settings.EmailsSentToday = count;
                settings.QuotaResetDateUtc = todayUtc;
            }
            else
            {
                settings.EmailsSentToday += count;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetRemainingDailyQuotaAsync()
        {
            var settings = await GetSettingsAsync();
            var todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (settings.QuotaResetDateUtc != todayUtc)
            {
                settings.EmailsSentToday = 0;
                settings.QuotaResetDateUtc = todayUtc;
                await _context.SaveChangesAsync();
            }

            var limit = settings.DailyEmailQuota > 0 ? settings.DailyEmailQuota : 300;
            return Math.Max(0, limit - settings.EmailsSentToday);
        }
    }
}
