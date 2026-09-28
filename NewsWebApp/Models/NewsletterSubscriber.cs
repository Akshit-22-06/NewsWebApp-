using System;
using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a subscriber to the daily news briefing newsletter.
    /// </summary>
    public class NewsletterSubscriber
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}
