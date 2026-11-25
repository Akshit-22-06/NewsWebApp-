using System.Collections.Generic;
using NewsWebApp.Models;
using NewsWebApp.Services;

namespace NewsWebApp.ViewModels
{
    public class AdminNewsletterViewModel
    {
        public int ActiveSubscribersCount { get; set; }
        public int UnsubscribedCount { get; set; }
        public int TotalSubscribersCount { get; set; }
        public IEnumerable<NewsletterSubscriber> Subscribers { get; set; } = new List<NewsletterSubscriber>();
        public IEnumerable<NewsletterEdition> Editions { get; set; } = new List<NewsletterEdition>();
        public NewsletterSchedulerStatusDto SchedulerStatus { get; set; } = new();
        public NewsletterSetting Settings { get; set; } = new();
        public EmailQuotaInfo QuotaInfo { get; set; } = new();
        public string? SearchQuery { get; set; }
        public string ActiveTab { get; set; } = "dispatch"; // "dispatch" or "subscribers"
    }
}
