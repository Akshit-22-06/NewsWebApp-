using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a user comment on a news article.
    /// </summary>
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Comment text is required.")]
        [StringLength(1000, MinimumLength = 2, ErrorMessage = "Comment must be between 2 and 1000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Posted At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign Key & Navigation for News Article
        [Required]
        public int NewsArticleId { get; set; }

        [ForeignKey(nameof(NewsArticleId))]
        public virtual NewsArticle? NewsArticle { get; set; }

        // Foreign Key & Navigation for ApplicationUser
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
