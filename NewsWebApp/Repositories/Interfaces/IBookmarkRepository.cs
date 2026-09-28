using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    public interface IBookmarkRepository
    {
        Task<bool> IsBookmarkedAsync(string userId, int articleId);
        Task<bool> ToggleBookmarkAsync(string userId, int articleId);
        Task<IEnumerable<NewsArticle>> GetUserBookmarksAsync(string userId);
        Task<int> GetUserBookmarkCountAsync(string userId);
    }
}
