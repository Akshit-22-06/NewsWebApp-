using System;
using System.Threading.Tasks;
using NewsWebApp.Models;

namespace NewsWebApp.Services
{
    public class NewsletterSchedulerStatusDto
    {
        public bool IsRunning { get; set; } = true;
        public bool IsAutoSendEnabled { get; set; } = true;
        public int SendHour { get; set; } = 7;
        public int SendMinute { get; set; } = 0;
        public string TimeZoneId { get; set; } = "Asia/Kolkata";
        public DateTime? LastRunAtIst { get; set; }
        public DateTime? NextRunAtIst { get; set; }
        public string? LastRunStatus { get; set; }
        public string ActiveEmailProvider { get; set; } = string.Empty;
        public bool IsEmailProviderConfigured { get; set; }
        public int DailyEmailQuota { get; set; } = 300;
        public int EmailsSentToday { get; set; }
        public int RemainingQuotaToday { get; set; } = 300;
        public string? QuotaAccountEmail { get; set; }
        public string? QuotaPlanType { get; set; }
        public bool IsLiveQuotaVerified { get; set; }
    }

    public interface INewsletterSchedulerManager
    {
        Task<EmailQuotaInfo> GetEmailQuotaAsync();
        /// <summary>
        /// Generates (or re-generates) an edition for the given IST date (defaults to today).
        /// Saves as Draft in database without emailing subscribers.
        /// </summary>
        Task<(bool Success, string Message, NewsletterEdition? Edition)> GenerateEditionAsync(
            string? editionDate = null, 
            string? baseUrl = null);

        /// <summary>
        /// Sends an existing Draft edition to all active subscribers.
        /// </summary>
        Task<(bool Success, string Message)> SendEditionAsync(
            int editionId, 
            string? baseUrl = null);

        /// <summary>
        /// Sends a preview of the edition to a single specified test email (e.g. admin).
        /// </summary>
        Task<(bool Success, string Message)> SendTestEmailAsync(
            int editionId, 
            string testEmail, 
            string? baseUrl = null);

        /// <summary>
        /// Returns real-time scheduler state and next run countdown.
        /// </summary>
        NewsletterSchedulerStatusDto GetStatus();

        /// <summary>
        /// Subscribes an email to The Morning Dispatch and dispatches an editorial welcome email.
        /// </summary>
        Task<(bool Success, string Message)> SubscribeAndSendWelcomeAsync(
            string email, 
            string? baseUrl = null);
    }
}
