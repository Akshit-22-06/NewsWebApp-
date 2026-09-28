using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    public interface INewsletterRepository
    {
        Task<(bool Success, string Message)> SubscribeAsync(string email);
        Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync();
        Task<int> GetSubscriberCountAsync();
        Task<bool> DeleteAsync(int id);
    }
}
