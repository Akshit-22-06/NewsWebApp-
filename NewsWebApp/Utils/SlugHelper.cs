using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewsWebApp.Utils
{
    /// <summary>
    /// Simple helper to generate URL-friendly slugs from article titles.
    /// Example: "Hello World! 2026" -> "hello-world-2026"
    /// </summary>
    public static class SlugHelper
    {
        public static string GenerateSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Guid.NewGuid().ToString("n")[..8];

            // 1. Convert to lowercase
            string slug = text.ToLower().Trim();

            // 2. Replace any non-alphanumeric characters with a hyphen
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // 3. Replace multiple spaces or hyphens with a single hyphen
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');

            return string.IsNullOrEmpty(slug) ? "post-" + Guid.NewGuid().ToString("n")[..6] : slug;
        }

        public static async Task<string> GenerateUniqueSlugAsync(string text, Func<string, Task<bool>> slugExistsAsync)
        {
            string baseSlug = GenerateSlug(text);
            string slug = baseSlug;
            int counter = 1;

            // If slug already exists in database, append -1, -2, etc.
            while (await slugExistsAsync(slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }
    }
}
