using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Tracks individual article view events for real-time trending calculations.
    /// Uses minimal hashed identifiers to prevent inflation while respecting user privacy.
    /// </summary>
    public class ArticleView
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int NewsArticleId { get; set; }

        [ForeignKey(nameof(NewsArticleId))]
        public virtual NewsArticle NewsArticle { get; set; } = null!;

        [Required]
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        [StringLength(32)]
        public string? ViewerHash { get; set; }
    }
}
