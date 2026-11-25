using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NewsWebApp.Models
{
    /// <summary>
    /// Configuration options for Google Gemini AI article moderation.
    /// </summary>
    public class GeminiModerationOptions
    {
        public const string SectionName = "Gemini";

        public string Model { get; set; } = "gemini-3.6-flash";
        public List<string> FallbackModels { get; set; } = new()
        {
            "gemini-3.5-flash",
            "gemini-3.5-flash-lite",
            "gemini-3.1-flash-lite",
            "gemini-flash-latest"
        };
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 25;
        public int MinimumQualityScore { get; set; } = 50;
    }

    /// <summary>
    /// Parsed and validated moderation result returned by the internal Gemini moderation engine.
    /// </summary>
    public class GeminiModerationResult
    {
        public bool IsSuccess { get; set; }
        public string Decision { get; set; } = "REJECT"; // strictly "APPROVE" or "REJECT"
        public bool IsAppropriate { get; set; }
        public int QualityScore { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Reasons { get; set; } = new List<string>();
        public string AuthorMessage { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime ModeratedAt { get; set; } = DateTime.UtcNow;

        public static GeminiModerationResult CreateFailure(string technicalError)
        {
            return new GeminiModerationResult
            {
                IsSuccess = false,
                Decision = "REJECT",
                IsAppropriate = false,
                QualityScore = 0,
                Categories = new List<string> { "SERVICE_FAILURE" },
                Reasons = new List<string> { technicalError },
                AuthorMessage = "We could not review your article right now. Please try again later.",
                ErrorMessage = technicalError,
                ModelVersion = "failed",
                ModeratedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Direct DTO mapping the expected structured JSON response from Gemini.
    /// </summary>
    public class GeminiResponseDto
    {
        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("isAppropriate")]
        public bool? IsAppropriate { get; set; }

        [JsonPropertyName("qualityScore")]
        public int? QualityScore { get; set; }

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }

        [JsonPropertyName("reasons")]
        public List<string>? Reasons { get; set; }

        [JsonPropertyName("authorMessage")]
        public string? AuthorMessage { get; set; }
    }
}
