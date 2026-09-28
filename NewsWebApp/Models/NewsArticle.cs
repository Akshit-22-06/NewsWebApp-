using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a news article entity in the system.
    /// </summary>
    public class NewsArticle
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string Slug { get; set; } = string.Empty;

        [Required(ErrorMessage = "Summary is required.")]
        [StringLength(500, ErrorMessage = "Summary cannot exceed 500 characters.")]
        [DataType(DataType.MultilineText)]
        public string Summary { get; set; } = string.Empty;

        [Required(ErrorMessage = "Article content is required.")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Image URL")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Published Date")]
        public DateTime? PublishedDate { get; set; }

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Is Published")]
        public bool IsPublished { get; set; } = false;

        [Display(Name = "Is Featured")]
        public bool IsFeatured { get; set; } = false;

        [Display(Name = "View Count")]
        public int ViewCount { get; set; } = 0;

        [StringLength(100)]
        [Display(Name = "Source")]
        public string? SourceName { get; set; }

        [StringLength(500)]
        [Display(Name = "Original Source Link")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string? SourceUrl { get; set; }

        [Display(Name = "Is Live Synced")]
        public bool IsLiveSynced { get; set; } = false;

        [Display(Name = "Like Count")]
        public int LikeCount { get; set; } = 0;

        // Foreign Key & Navigation for Category
        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; }

        // Foreign Key & Navigation for Author
        [Required]
        [Display(Name = "Author")]
        public int AuthorId { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public virtual Author? Author { get; set; }

        // Navigation property for Comments
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        // Navigation property for Bookmarks
        public virtual ICollection<ArticleBookmark> Bookmarks { get; set; } = new List<ArticleBookmark>();
    }
}
