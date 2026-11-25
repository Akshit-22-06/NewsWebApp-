using System.Threading;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Service for automatically moderating and evaluating news article submissions using Google Gemini API.
    /// </summary>
    public interface IGeminiModerationService
    {
        Task<GeminiModerationResult> ModerateArticleAsync(
            string title, 
            string summary, 
            string content, 
            CancellationToken cancellationToken = default);
    }
}
