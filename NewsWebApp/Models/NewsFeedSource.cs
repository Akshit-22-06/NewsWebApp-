using System;
using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a dynamic RSS/Atom or API news feed source configured in the system.
    /// Admins can add, manage, and synchronize feeds directly from the dashboard.
    /// </summary>
    public class NewsFeedSource
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Source name is required.")]
        [StringLength(100, ErrorMessage = "Source name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Feed URL or API endpoint is required.")]
        [StringLength(500, ErrorMessage = "Feed URL cannot exceed 500 characters.")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string FeedUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category slug is required.")]
        [StringLength(100)]
        public string CategorySlug { get; set; } = "general";

        [StringLength(500)]
        public string? DefaultImageUrl { get; set; }

        [StringLength(100)]
        [Display(Name = "Region / Location")]
        public string Region { get; set; } = "Worldwide";

        [StringLength(50)]
        [Display(Name = "Location Scope")]
        public string LocationScope { get; set; } = "Global";

        public bool IsActive { get; set; } = true;

        public DateTime? LastSyncedAt { get; set; }

        public int TotalArticlesImported { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
