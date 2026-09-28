using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    /// <summary>
    /// Contract for comment data access operations.
    /// </summary>
    public interface ICommentRepository
    {
        Task<IEnumerable<Comment>> GetByArticleIdAsync(int articleId);
        Task<Comment?> GetByIdAsync(int id);
        Task<IEnumerable<Comment>> GetAllAsync();
        Task<IEnumerable<Comment>> GetRecentAsync(int count);
        Task AddAsync(Comment comment);
        Task DeleteAsync(int id);
        Task<int> GetTotalCountAsync();
    }
}
