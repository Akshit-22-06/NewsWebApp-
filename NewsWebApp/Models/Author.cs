using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents an author profile linked to an ASP.NET Core Identity user.
    /// </summary>
    public class Author
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Display name is required.")]
        [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters.")]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
        [DataType(DataType.MultilineText)]
        public string? Bio { get; set; }

        [StringLength(250)]
        [Display(Name = "Profile Image URL")]
        public string? ProfileImageUrl { get; set; }

        // Navigation property: One author can write many news articles
        public virtual ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();
    }
}
