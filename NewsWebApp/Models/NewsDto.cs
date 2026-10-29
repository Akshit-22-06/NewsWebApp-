using System;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Data Transfer Object representing a news article from Currents API.
    /// Contains only the essential fields needed by the application.
    /// </summary>
    public class NewsDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? Image { get; set; }
        public string? Author { get; set; }
        public DateTime? Published { get; set; }
        public string? Category { get; set; }
        public string? Language { get; set; }
    }
}
