using System.ComponentModel.DataAnnotations;

namespace NewsWebApp.ViewModels
{
    /// <summary>
    /// ViewModel for submitting a new comment on a news article.
    /// </summary>
    public class AddCommentViewModel
    {
        [Required]
        public int NewsArticleId { get; set; }

        [Required]
        public string ArticleSlug { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please write a comment before submitting.")]
        [StringLength(1000, MinimumLength = 2, ErrorMessage = "Comment must be between 2 and 1000 characters.")]
        [Display(Name = "Your Comment")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; } = string.Empty;
    }
}
