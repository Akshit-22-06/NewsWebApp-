using System;
using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a generated daily edition of The Morning Dispatch newsletter.
    /// Tracks editorial content, curation metadata, and batch delivery statistics.
    /// </summary>
    public class NewsletterEdition
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string EditionDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd" (IST date)

        [Required]
        [StringLength(250)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string HtmlContent { get; set; } = string.Empty;

        [Required]
        public string PlainTextContent { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, Sent, Failed

        public int TotalRecipients { get; set; }

        public int SuccessfulDeliveries { get; set; }

        public int FailedDeliveries { get; set; }

        public int QueuedCount { get; set; } // Recipients queued if daily provider limit hit

        public string? ErrorDetails { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public string? CuratedJson { get; set; }
    }
}
