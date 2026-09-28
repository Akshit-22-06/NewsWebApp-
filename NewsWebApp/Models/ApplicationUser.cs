using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Represents an authenticated user in the system, extending ASP.NET Core IdentityUser.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
        public string? Bio { get; set; }

        [StringLength(250)]
        [Display(Name = "Profile Picture URL")]
        public string? ProfilePictureUrl { get; set; }

        [Display(Name = "Member Since")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Author? Author { get; set; }
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<ArticleBookmark> Bookmarks { get; set; } = new List<ArticleBookmark>();
    }
}
