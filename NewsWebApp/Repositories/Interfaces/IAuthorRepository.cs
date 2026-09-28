using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    /// <summary>
    /// Contract for author data access operations.
    /// </summary>
    public interface IAuthorRepository
    {
        Task<IEnumerable<Author>> GetAllAsync();
        Task<Author?> GetByIdAsync(int id);
        Task<Author?> GetByUserIdAsync(string userId);
        Task AddAsync(Author author);
        Task UpdateAsync(Author author);
        Task DeleteAsync(int id);
    }
}
