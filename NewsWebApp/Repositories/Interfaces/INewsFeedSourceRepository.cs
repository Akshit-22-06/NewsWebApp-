using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    public interface INewsFeedSourceRepository
    {
        Task<IEnumerable<NewsFeedSource>> GetAllAsync();
        Task<IEnumerable<NewsFeedSource>> GetActiveAsync();
        Task<NewsFeedSource?> GetByIdAsync(int id);
        Task<NewsFeedSource?> GetByUrlAsync(string url);
        Task AddAsync(NewsFeedSource source);
        Task UpdateAsync(NewsFeedSource source);
        Task DeleteAsync(int id);
        Task<bool> ToggleActiveAsync(int id);
        Task RecordSyncAsync(int id, int articlesImported);
    }
}
