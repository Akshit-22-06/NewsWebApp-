using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for creating or updating a news category in the admin panel.
    /// </summary>
    public class CategoryViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Slug cannot exceed 100 characters.")]
        [Display(Name = "Custom Slug (Optional, auto-generated if left blank)")]
        public string? Slug { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Category Description")]
        public string? Description { get; set; }
    }
}
