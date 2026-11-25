using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    public interface INewsletterRepository
    {
        // Subscriber Management
        Task<(bool Success, string Message, bool IsNewOrReactivated, NewsletterSubscriber? Subscriber)> SubscribeAsync(string email);
        Task<(bool Success, string Message, NewsletterSubscriber? Subscriber)> UnsubscribeByTokenAsync(string token);
        Task<NewsletterSubscriber?> GetByTokenAsync(string token);
        Task<NewsletterSubscriber?> GetByEmailAsync(string email);
        Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync(string? search = null);
        Task<List<NewsletterSubscriber>> GetActiveSubscribersAsync();
        Task<int> GetActiveSubscriberCountAsync();
        Task<int> GetSubscriberCountAsync();
        Task<int> GetUnsubscribedCountAsync();
        Task<bool> DeleteAsync(int id);
        Task<bool> ToggleStatusAsync(int id);

        // Edition Management
        Task<NewsletterEdition?> GetEditionByDateAsync(string editionDate);
        Task<NewsletterEdition?> GetLatestEditionAsync();
        Task<NewsletterEdition?> GetEditionByIdAsync(int id);
        Task<List<NewsletterEdition>> GetAllEditionsAsync(int take = 30);
        Task<NewsletterEdition> SaveEditionAsync(NewsletterEdition edition);

        // Configuration & Scheduling Settings
        Task<NewsletterSetting> GetSettingsAsync();
        Task<NewsletterSetting> UpdateSettingsAsync(NewsletterSetting settings);

        // Brevo Quota & Idempotent Delivery Tracking
        Task<List<NewsletterSubscriber>> GetPendingSubscribersForEditionAsync(int editionId);
        Task MarkSubscribersDeliveredAsync(IEnumerable<int> subscriberIds, int editionId);
        Task RecordEmailsSentAsync(int count);
        Task<int> GetRemainingDailyQuotaAsync();
    }
}
