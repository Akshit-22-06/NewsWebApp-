using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NewsWebApp.Services
{
    public class CuratedStoryDto
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? PublishedAt { get; set; }
    }

    public class CuratedCategoryGroupDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public List<CuratedStoryDto> Stories { get; set; } = new();
    }

    public class CuratedEventDto
    {
        public string EventName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Time { get; set; }
        public string? Location { get; set; }
        public string WhyItMatters { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class CuratedNewsletterResult
    {
        public string EditionDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string FormattedDate { get; set; } = string.Empty; // e.g. "Monday, November 2, 2026"
        public string Subject { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public List<CuratedStoryDto> TopHeadlines { get; set; } = new();
        public List<CuratedCategoryGroupDto> CategorizedNews { get; set; } = new();
        public List<CuratedEventDto> TodayEvents { get; set; } = new();
        public string HtmlTemplate { get; set; } = string.Empty; // Contains {{UNSUBSCRIBE_URL}}
        public string PlainTextTemplate { get; set; } = string.Empty;
        public string CuratedJson { get; set; } = string.Empty;
        public bool UsedAiCuration { get; set; }
    }

    public interface INewsletterCurationService
    {
        /// <summary>
        /// Curates all sections of The Morning Dispatch for the specified date (IST).
        /// Gathers news from Currents API and local reporting, applies Gemini AI curation, and produces email templates.
        /// </summary>
        Task<CuratedNewsletterResult> CurateDailyEditionAsync(
            DateTime? targetDateIst = null, 
            string? baseUrl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Renders the personalized HTML body for a specific subscriber.
        /// </summary>
        string RenderPersonalizedHtml(string htmlTemplate, string unsubscribeToken, string baseUrl);

        /// <summary>
        /// Renders the personalized plain-text body for a specific subscriber.
        /// </summary>
        string RenderPersonalizedPlainText(string plainTextTemplate, string unsubscribeToken, string baseUrl);
    }
}
