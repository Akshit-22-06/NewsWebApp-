using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Repositories.Interfaces
{
    /// <summary>
    /// Contract for news category data access operations.
    /// </summary>
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(int id);
        Task<Category?> GetBySlugAsync(string slug);
        Task AddAsync(Category category);
        Task UpdateAsync(Category category);
        Task DeleteAsync(int id);
        Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
        Task<int> GetTotalCountAsync();
        Task<IEnumerable<(Category Category, int ArticleCount)>> GetCategoriesWithCountAsync();
    }
}
