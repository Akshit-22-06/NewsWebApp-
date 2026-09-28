using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents a news category (e.g. Technology, Sports, Politics).
    /// </summary>
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [StringLength(100, ErrorMessage = "Slug cannot exceed 100 characters.")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        // Navigation property: One category has many news articles
        public virtual ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();
    }
}
