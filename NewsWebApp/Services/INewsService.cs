using System.Collections.Generic;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Service contract for communicating with Currents News API.
    /// Provides simple, beginner-friendly methods for retrieving recent news, searching, and filtering.
    /// </summary>
    public interface INewsService
    {
        Task<List<NewsDto>> GetLatestNewsAsync(string? category = null, string? country = null, string? language = null);
        Task<List<NewsDto>> SearchNewsAsync(string query, string? category = null, string? country = null, string? language = null);
        Task<List<NewsDto>> GetNewsByCategoryAsync(string category);
    }
}
