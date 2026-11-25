using System;
using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Configuration settings for the automated daily morning newsletter dispatch.
    /// </summary>
    public class NewsletterSetting
    {
        [Key]
        public int Id { get; set; }

        public bool IsAutoSendEnabled { get; set; } = true;

        public int SendHour { get; set; } = 7; // 7 AM IST

        public int SendMinute { get; set; } = 0;

        [StringLength(50)]
        public string TimeZoneId { get; set; } = "Asia/Kolkata";

        [StringLength(100)]
        public string SenderName { get; set; } = "The Morning Dispatch | NewsBase";

        [StringLength(200)]
        public string SenderEmail { get; set; } = "newsletter@newsbase.org";

        [StringLength(200)]
        public string? AdminTestEmail { get; set; }

        public DateTime? LastRunAt { get; set; }

        public DateTime? NextRunAt { get; set; }

        [StringLength(100)]
        public string? LastRunStatus { get; set; }

        public int DailyEmailQuota { get; set; } = 300; // Brevo Free Tier default: 300 emails/day

        public int EmailsSentToday { get; set; } = 0;

        [StringLength(20)]
        public string? QuotaResetDateUtc { get; set; } // Format: yyyy-MM-dd in UTC to track daily reset
    }
}
