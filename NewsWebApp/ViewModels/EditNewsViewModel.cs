using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for editing an existing news article.
    /// </summary>
    public class EditNewsViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [StringLength(250, ErrorMessage = "Slug cannot exceed 250 characters.")]
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

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Display(Name = "Is Published")]
        public bool IsPublished { get; set; }

        [Display(Name = "Is Featured")]
        public bool IsFeatured { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}
