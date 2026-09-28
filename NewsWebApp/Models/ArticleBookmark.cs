using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a saved/bookmarked news article for a user.
    /// </summary>
    public class ArticleBookmark
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        public int NewsArticleId { get; set; }

        [ForeignKey(nameof(NewsArticleId))]
        public virtual NewsArticle? NewsArticle { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
