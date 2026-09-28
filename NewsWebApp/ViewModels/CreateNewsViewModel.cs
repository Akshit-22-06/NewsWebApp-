using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for creating a new article from the author/admin panel.
    /// </summary>
    public class CreateNewsViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Custom Slug (Optional, auto-generated if left blank)")]
        public string? Slug { get; set; }

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

        [Display(Name = "Publish Immediately")]
        public bool IsPublished { get; set; } = true;

        [Display(Name = "Mark as Featured")]
        public bool IsFeatured { get; set; } = false;

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}
