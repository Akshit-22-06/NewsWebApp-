using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewsWebApp.Services
{
    public class EmailQuotaInfo
    {
        public int DailyLimit { get; set; } = 300; // Brevo free tier limit
        public int UsedToday { get; set; }
        public int RemainingToday => Math.Max(0, DailyLimit - UsedToday);
        public string Provider { get; set; } = "Brevo (Free Tier: 300 emails/day)";
        public string ResetTimeDescription { get; set; } = "Resets daily at 00:00 UTC (05:30 AM IST)";
        public bool IsLiveAccountChecked { get; set; }
        public string? AccountEmail { get; set; }
        public string? PlanType { get; set; }
        public double? LiveCreditsRemaining { get; set; }
    }

    public class BatchDeliveryResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int QueuedCount { get; set; }
        public bool HitQuotaLimit { get; set; }
        public List<int> DeliveredSubscriberIds { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public interface IEmailService
    {
        /// <summary>
        /// Sends a single email to a specific recipient (e.g. for admin test dispatches).
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> SendEmailAsync(
            string toEmail, 
            string subject, 
            string htmlContent, 
            string plainTextContent);

        /// <summary>
        /// Sends batch emails to active subscribers with personalized unsubscribe links.
        /// Strictly respects the Brevo free daily quota (300/day) and stops when quota is exhausted.
        /// </summary>
        Task<BatchDeliveryResult> SendBatchAsync(
            IEnumerable<(int Id, string Email, string UnsubscribeToken)> recipients,
            string subject,
            int maxDailyQuotaRemaining,
            Func<string, string> htmlBodyFactory,
            Func<string, string> plainTextBodyFactory);

        /// <summary>
        /// Retrieves the current Brevo daily email quota, live account credits (if configured), and reset time.
        /// </summary>
        Task<EmailQuotaInfo> GetDailyQuotaAsync(int localUsedToday = 0, int localDailyLimit = 300);

        /// <summary>
        /// Returns the name of the active email provider (e.g. "Brevo", "Resend", or "Simulated/Test").
        /// </summary>
        string GetProviderName();

        /// <summary>
        /// Checks whether real API credentials have been configured.
        /// </summary>
        bool IsConfigured();
    }
}
