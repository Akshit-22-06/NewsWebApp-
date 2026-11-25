using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;

namespace NewsWebApp.Services
{
    public class NewsletterCurationService : INewsletterCurationService
    {
        private readonly INewsService _newsService;
        private readonly INewsRepository _newsRepository;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewsletterCurationService> _logger;

        private readonly string? _geminiApiKey;
        private readonly string _geminiModel;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public NewsletterCurationService(
            INewsService newsService,
            INewsRepository newsRepository,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<NewsletterCurationService> logger)
        {
            _newsService = newsService;
            _newsRepository = newsRepository;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _geminiApiKey = configuration["Gemini:ApiKey"] 
                ?? configuration["GEMINI_API_KEY"] 
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            _geminiModel = configuration["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        }

        public async Task<CuratedNewsletterResult> CurateDailyEditionAsync(
            DateTime? targetDateIst = null, 
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            baseUrl ??= "http://localhost:5235";
            var istZone = GetIndiaTimeZone();
            var nowIst = targetDateIst ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var yesterdayIst = nowIst.AddDays(-1);

            var editionDateStr = nowIst.ToString("yyyy-MM-dd");
            var formattedDate = nowIst.ToString("dddd, MMMM d, yyyy");

            _logger.LogInformation("Beginning curation for The Morning Dispatch (Edition: {Date})...", editionDateStr);

            // Step 1: Collect Candidate News Stories from Currents API and Local Articles
            var candidateStories = await GatherCandidateNewsAsync(yesterdayIst, nowIst);
            var candidateEvents = await GatherCandidateEventsAsync(nowIst);

            _logger.LogInformation("Gathered {StoryCount} candidate stories and {EventCount} candidate events.",
                candidateStories.Count, candidateEvents.Count);

            // Step 2: Curation via Gemini AI (or Fallback if API fails/unavailable)
            CuratedNewsletterResult result;
            if (!string.IsNullOrWhiteSpace(_geminiApiKey) && candidateStories.Count > 0)
            {
                try
                {
                    result = await CurateWithGeminiAsync(candidateStories, candidateEvents, formattedDate, editionDateStr, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini AI curation encountered an error. Falling back to rule-based editorial curation.");
                    result = BuildFallbackCuration(candidateStories, candidateEvents, formattedDate, editionDateStr);
                }
            }
            else
            {
                _logger.LogInformation("Gemini API key missing or no candidate stories found. Using rule-based curation.");
                result = BuildFallbackCuration(candidateStories, candidateEvents, formattedDate, editionDateStr);
            }

            // Step 3: Generate Email Templates (HTML and Plain Text)
            result.HtmlTemplate = GenerateHtmlTemplate(result, baseUrl);
            result.PlainTextTemplate = GeneratePlainTextTemplate(result, baseUrl);

            return result;
        }

        public string RenderPersonalizedHtml(string htmlTemplate, string unsubscribeToken, string baseUrl)
        {
            var unsubscribeUrl = $"{baseUrl.TrimEnd('/')}/newsletter/unsubscribe?token={Uri.EscapeDataString(unsubscribeToken)}";
            return htmlTemplate.Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl);
        }

        public string RenderPersonalizedPlainText(string plainTextTemplate, string unsubscribeToken, string baseUrl)
        {
            var unsubscribeUrl = $"{baseUrl.TrimEnd('/')}/newsletter/unsubscribe?token={Uri.EscapeDataString(unsubscribeToken)}";
            return plainTextTemplate.Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl);
        }

        // ==========================================
        // 1. NEWS COLLECTION
        // ==========================================

        private async Task<List<CandidateStory>> GatherCandidateNewsAsync(DateTime yesterdayIst, DateTime todayIst)
        {
            var list = new List<CandidateStory>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Local approved journalism from NewsBase (priority original reporting)
            try
            {
                var localArticles = await _newsRepository.GetLatestAsync(10);
                foreach (var a in localArticles)
                {
                    if (!string.IsNullOrWhiteSpace(a.Title) && seenTitles.Add(a.Title))
                    {
                        list.Add(new CandidateStory
                        {
                            Title = a.Title,
                            Description = a.Summary,
                            Category = a.Category?.Name ?? "General",
                            Source = "NewsBase Editorial",
                            Url = $"/news/details/{a.Slug}",
                            Published = a.PublishedDate?.ToString("yyyy-MM-dd HH:mm")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch local articles for newsletter candidate pool.");
            }

            // 2. Query Currents API for India News
            try
            {
                var indiaNews = await _newsService.SearchNewsAsync("", country: "IN", language: "en");
                foreach (var n in indiaNews.Take(8))
                {
                    if (!string.IsNullOrWhiteSpace(n.Title) && seenTitles.Add(n.Title))
                    {
                        list.Add(new CandidateStory
                        {
                            Title = n.Title,
                            Description = n.Description ?? string.Empty,
                            Category = "India News",
                            Source = n.Author ?? "National Wire",
                            Url = n.Url ?? "#",
                            Published = n.Published?.ToString("yyyy-MM-dd HH:mm")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch India news from Currents API.");
            }

            // 3. Query Currents API for Key Standard Categories
            var categories = new[] { "technology", "politics", "business", "world", "science", "sports", "entertainment" };
            foreach (var cat in categories)
            {
                try
                {
                    var items = await _newsService.GetNewsByCategoryAsync(cat);
                    foreach (var n in items.Take(4))
                    {
                        if (!string.IsNullOrWhiteSpace(n.Title) && seenTitles.Add(n.Title))
                        {
                            list.Add(new CandidateStory
                            {
                                Title = n.Title,
                                Description = n.Description ?? string.Empty,
                                Category = MapCurrentsCategory(cat),
                                Source = n.Author ?? "International Wire",
                                Url = n.Url ?? "#",
                                Published = n.Published?.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch category '{Category}' from Currents API.", cat);
                }
            }

            return list;
        }

        private async Task<List<CandidateEvent>> GatherCandidateEventsAsync(DateTime todayIst)
        {
            var events = new List<CandidateEvent>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Search for confirmed scheduled events / summits / launches / elections
                var searchResults = await _newsService.SearchNewsAsync("scheduled OR summit OR match OR election OR launch", language: "en");
                foreach (var item in searchResults.Take(10))
                {
                    if (!string.IsNullOrWhiteSpace(item.Title) && seen.Add(item.Title))
                    {
                        events.Add(new CandidateEvent
                        {
                            Title = item.Title,
                            Description = item.Description ?? string.Empty,
                            Source = item.Author ?? "Official Wire",
                            Url = item.Url ?? "#"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not search for scheduled events.");
            }

            return events;
        }

        private static string MapCurrentsCategory(string cat)
        {
            return cat.ToLowerInvariant() switch
            {
                "technology" => "Technology",
                "politics" => "Politics",
                "business" => "Business and Finance",
                "finance" => "Business and Finance",
                "world" => "World News",
                "science" => "Science and Innovation",
                "sports" => "Sports",
                "entertainment" => "Entertainment",
                _ => "General"
            };
        }

        // ==========================================
        // 2. GEMINI AI CURATION
        // ==========================================

        private async Task<CuratedNewsletterResult> CurateWithGeminiAsync(
            List<CandidateStory> stories, 
            List<CandidateEvent> events, 
            string formattedDate, 
            string editionDateStr,
            CancellationToken cancellationToken)
        {
            var systemInstruction = @"You are the chief editorial director for NewsBase's prestigious morning briefing: 'The Morning Dispatch'.
Your task is to analyze collected news stories and scheduled event reports, and curate a distinguished, concise, and accurate daily newspaper edition.

CRITICAL RULES:
1. Summarize ONLY from the provided candidate stories and events.
2. NEVER invent facts, quotes, statistics, or events.
3. Preserve the exact source URL and publisher name for every single story.
4. SECTION C: Select 3 to 5 major headlines across all categories that readers should know first.
5. SECTION A: Organize yesterday's news into these categories:
   - Technology
   - Politics
   - Business and Finance
   - World News
   - India News
   - Science and Innovation
   - Sports
   - Entertainment
   Include approximately 2-3 stories per category with a clear, concise 2-sentence journalistic summary.
6. SECTION B: Include confirmed events scheduled or happening today. If no verified scheduled events exist in the provided sources, return an empty array. Do NOT invent events.
7. Tone: Objective, authoritative, dignified editorial voice. No clickbait or sensationalism.";

            var inputPayload = new
            {
                editionDate = formattedDate,
                stories = stories.Take(30).Select(s => new { s.Title, s.Description, s.Category, s.Source, s.Url, s.Published }),
                events = events.Take(10).Select(e => new { e.Title, e.Description, e.Source, e.Url })
            };

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new object[] { new { text = systemInstruction } }
                },
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = $"Curate The Morning Dispatch for {formattedDate}.\n\nCANDIDATE DATA:\n{JsonSerializer.Serialize(inputPayload)}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            subject = new { type = "STRING" },
                            introduction = new { type = "STRING" },
                            topHeadlines = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        title = new { type = "STRING" },
                                        summary = new { type = "STRING" },
                                        category = new { type = "STRING" },
                                        source = new { type = "STRING" },
                                        url = new { type = "STRING" }
                                    },
                                    required = new[] { "title", "summary", "category", "source", "url" }
                                }
                            },
                            categorizedNews = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        categoryName = new { type = "STRING" },
                                        stories = new
                                        {
                                            type = "ARRAY",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    title = new { type = "STRING" },
                                                    summary = new { type = "STRING" },
                                                    source = new { type = "STRING" },
                                                    url = new { type = "STRING" },
                                                    publishedAt = new { type = "STRING" }
                                                },
                                                required = new[] { "title", "summary", "source", "url" }
                                            }
                                        }
                                    },
                                    required = new[] { "categoryName", "stories" }
                                }
                            },
                            todayEvents = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        eventName = new { type = "STRING" },
                                        description = new { type = "STRING" },
                                        time = new { type = "STRING" },
                                        location = new { type = "STRING" },
                                        whyItMatters = new { type = "STRING" },
                                        source = new { type = "STRING" },
                                        url = new { type = "STRING" }
                                    },
                                    required = new[] { "eventName", "description", "whyItMatters", "source", "url" }
                                }
                            }
                        },
                        required = new[] { "subject", "introduction", "topHeadlines", "categorizedNews", "todayEvents" }
                    }
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModel}:generateContent?key={_geminiApiKey}";

            using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Gemini returned HTTP {response.StatusCode}: {err}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

            var parsed = JsonSerializer.Deserialize<GeminiCuratedNewsletterDto>(text!, JsonOpts)
                ?? throw new InvalidOperationException("Failed to parse Gemini newsletter JSON response.");

            return new CuratedNewsletterResult
            {
                EditionDate = editionDateStr,
                FormattedDate = formattedDate,
                Subject = parsed.Subject ?? $"The Morning Dispatch — {formattedDate}",
                Introduction = parsed.Introduction ?? "Good morning! Here is your daily briefing of the most important stories from yesterday and key events to watch today.",
                TopHeadlines = parsed.TopHeadlines ?? new List<CuratedStoryDto>(),
                CategorizedNews = parsed.CategorizedNews ?? new List<CuratedCategoryGroupDto>(),
                TodayEvents = parsed.TodayEvents ?? new List<CuratedEventDto>(),
                CuratedJson = text ?? "{}",
                UsedAiCuration = true
            };
        }

        // ==========================================
        // 3. RULE-BASED FALLBACK CURATION
        // ==========================================

        private CuratedNewsletterResult BuildFallbackCuration(
            List<CandidateStory> stories, 
            List<CandidateEvent> events, 
            string formattedDate, 
            string editionDateStr)
        {
            var topStories = stories.Take(4).Select(s => new CuratedStoryDto
            {
                Title = s.Title,
                Summary = !string.IsNullOrWhiteSpace(s.Description) ? Truncate(s.Description, 160) : "Reporting on significant national and global developments.",
                Category = s.Category,
                Source = s.Source,
                Url = s.Url,
                PublishedAt = s.Published
            }).ToList();

            var categorized = new List<CuratedCategoryGroupDto>();
            var targetCategories = new[]
            {
                "India News", "Technology", "Politics", "Business and Finance",
                "World News", "Science and Innovation", "Sports", "Entertainment"
            };

            foreach (var cat in targetCategories)
            {
                var groupStories = stories
                    .Where(s => string.Equals(s.Category, cat, StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .Select(s => new CuratedStoryDto
                    {
                        Title = s.Title,
                        Summary = !string.IsNullOrWhiteSpace(s.Description) ? Truncate(s.Description, 140) : "Key updates on this developing story.",
                        Category = cat,
                        Source = s.Source,
                        Url = s.Url,
                        PublishedAt = s.Published
                    }).ToList();

                if (groupStories.Count > 0)
                {
                    categorized.Add(new CuratedCategoryGroupDto
                    {
                        CategoryName = cat,
                        Stories = groupStories
                    });
                }
            }

            var todayEventList = events.Take(3).Select(e => new CuratedEventDto
            {
                EventName = e.Title,
                Description = !string.IsNullOrWhiteSpace(e.Description) ? Truncate(e.Description, 150) : "Scheduled briefing or tournament event today.",
                WhyItMatters = "Scheduled development impacting policy, business, or public interest.",
                Source = e.Source,
                Url = e.Url
            }).ToList();

            return new CuratedNewsletterResult
            {
                EditionDate = editionDateStr,
                FormattedDate = formattedDate,
                Subject = topStories.Count > 0 
                    ? $"The Morning Dispatch: {topStories[0].Title} — {formattedDate}"
                    : $"The Morning Dispatch — {formattedDate}",
                Introduction = "Good morning! Here is your daily briefing of verified reporting from yesterday and key events to watch today.",
                TopHeadlines = topStories,
                CategorizedNews = categorized,
                TodayEvents = todayEventList,
                CuratedJson = "{}",
                UsedAiCuration = false
            };
        }

        // ==========================================
        // 4. HTML EMAIL GENERATOR
        // ==========================================

        private static string GenerateHtmlTemplate(CuratedNewsletterResult data, string baseUrl)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>The Morning Dispatch</title>");
            sb.AppendLine("<!--[if mso]><style>body,table,td {font-family: Arial, Helvetica, sans-serif !important;}</style><![endif]-->");
            sb.AppendLine("</head>");
            sb.AppendLine("<body style=\"margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #0f172a; -webkit-font-smoothing: antialiased;\">");

            sb.AppendLine("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color: #f1f5f9; padding: 24px 0;\">");
            sb.AppendLine("<tr><td align=\"center\">");

            // Main Email Container
            sb.AppendLine("<table role=\"presentation\" width=\"620\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width: 620px; width: 100%; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 4px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.05);\">");

            // Newspaper Masthead Bar
            sb.AppendLine("<tr><td style=\"background-color: #0f172a; padding: 20px 28px; text-align: center;\">");
            sb.AppendLine("<div style=\"font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #94a3b8; font-weight: 600; margin-bottom: 4px;\">NewsBase &bull; Independent Daily Digest</div>");
            sb.AppendLine("<h1 style=\"margin: 0; font-family: Georgia, 'Times New Roman', serif; font-size: 28px; color: #ffffff; letter-spacing: 0.5px; font-weight: 700;\">The Morning Dispatch</h1>");
            sb.AppendLine($"<div style=\"font-size: 12px; color: #cbd5e1; margin-top: 6px;\">{data.FormattedDate} &bull; Morning Edition (Asia/Kolkata)</div>");
            sb.AppendLine("</td></tr>");

            // Thin Red Accent Line
            sb.AppendLine("<tr><td style=\"height: 3px; background-color: #991b1b;\"></td></tr>");

            // Content Area
            sb.AppendLine("<tr><td style=\"padding: 28px 32px;\">");

            // Editorial Introduction
            sb.AppendLine($"<p style=\"font-size: 15px; line-height: 1.6; color: #334155; margin: 0 0 24px 0; font-style: italic; border-left: 3px solid #991b1b; padding-left: 14px;\">{Escape(data.Introduction)}</p>");

            // SECTION C: Top Stories of the Day (Lead Items)
            if (data.TopHeadlines.Count > 0)
            {
                sb.AppendLine("<div style=\"margin-bottom: 28px; padding-bottom: 20px; border-bottom: 2px solid #0f172a;\">");
                sb.AppendLine("<h2 style=\"margin: 0 0 16px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 19px; color: #0f172a; text-transform: uppercase; letter-spacing: 1px; font-weight: 700;\">Top Stories of the Day</h2>");

                foreach (var story in data.TopHeadlines)
                {
                    sb.AppendLine("<div style=\"margin-bottom: 16px; padding: 14px 16px; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 4px;\">");
                    sb.AppendLine($"<div style=\"font-size: 11px; text-transform: uppercase; font-weight: 700; color: #991b1b; letter-spacing: 1px; margin-bottom: 4px;\">{Escape(story.Category)}</div>");
                    sb.AppendLine($"<h3 style=\"margin: 0 0 6px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 17px; line-height: 1.35;\"><a href=\"{Escape(story.Url)}\" target=\"_blank\" style=\"color: #0f172a; text-decoration: none;\">{Escape(story.Title)}</a></h3>");
                    sb.AppendLine($"<p style=\"margin: 0 0 6px 0; font-size: 13.5px; line-height: 1.5; color: #475569;\">{Escape(story.Summary)}</p>");
                    sb.AppendLine($"<div style=\"font-size: 11.5px; color: #64748b;\">Source: <strong>{Escape(story.Source)}</strong> &bull; <a href=\"{Escape(story.Url)}\" target=\"_blank\" style=\"color: #991b1b; text-decoration: underline;\">Read Full Story &rarr;</a></div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // SECTION A: Yesterday's Top News (Grouped by Category)
            if (data.CategorizedNews.Count > 0)
            {
                sb.AppendLine("<div style=\"margin-bottom: 28px;\">");
                sb.AppendLine("<h2 style=\"margin: 0 0 18px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 19px; color: #0f172a; text-transform: uppercase; letter-spacing: 1px; font-weight: 700;\">Yesterday's Top Reporting</h2>");

                foreach (var group in data.CategorizedNews)
                {
                    sb.AppendLine($"<div style=\"margin-bottom: 20px;\">");
                    sb.AppendLine($"<h3 style=\"margin: 0 0 10px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 15px; color: #991b1b; text-transform: uppercase; letter-spacing: 1.5px; font-weight: 700; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px;\">{Escape(group.CategoryName)}</h3>");

                    foreach (var story in group.Stories)
                    {
                        sb.AppendLine("<div style=\"margin-bottom: 12px; padding-left: 6px;\">");
                        sb.AppendLine($"<div style=\"font-size: 14.5px; font-weight: 600; line-height: 1.35;\"><a href=\"{Escape(story.Url)}\" target=\"_blank\" style=\"color: #0f172a; text-decoration: none;\">{Escape(story.Title)}</a></div>");
                        sb.AppendLine($"<div style=\"font-size: 13px; line-height: 1.45; color: #475569; margin: 3px 0;\">{Escape(story.Summary)}</div>");
                        sb.AppendLine($"<div style=\"font-size: 11px; color: #64748b;\">Source: {Escape(story.Source)} &bull; <a href=\"{Escape(story.Url)}\" target=\"_blank\" style=\"color: #991b1b; text-decoration: underline;\">Source Link</a></div>");
                        sb.AppendLine("</div>");
                    }
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // SECTION B: Important Events Happening Today
            if (data.TodayEvents.Count > 0)
            {
                sb.AppendLine("<div style=\"margin-bottom: 28px; padding: 18px 20px; background-color: #f8fafc; border: 1px solid #cbd5e1; border-radius: 4px;\">");
                sb.AppendLine("<h2 style=\"margin: 0 0 14px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 18px; color: #0f172a; text-transform: uppercase; letter-spacing: 1px; font-weight: 700;\">Important Events Happening Today</h2>");

                foreach (var evt in data.TodayEvents)
                {
                    sb.AppendLine("<div style=\"margin-bottom: 14px;\">");
                    sb.AppendLine($"<div style=\"font-size: 14px; font-weight: 700; color: #0f172a;\">&bull; {Escape(evt.EventName)}</div>");
                    sb.AppendLine($"<div style=\"font-size: 13px; color: #334155; line-height: 1.45; margin-top: 2px;\">{Escape(evt.Description)}</div>");
                    if (!string.IsNullOrWhiteSpace(evt.WhyItMatters))
                    {
                        sb.AppendLine($"<div style=\"font-size: 12px; color: #64748b; margin-top: 2px;\"><strong>Why it matters:</strong> {Escape(evt.WhyItMatters)}</div>");
                    }
                    sb.AppendLine($"<div style=\"font-size: 11px; color: #94a3b8; margin-top: 2px;\">Verified Source: <a href=\"{Escape(evt.Url)}\" target=\"_blank\" style=\"color: #991b1b; text-decoration: underline;\">{Escape(evt.Source)}</a></div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // Editorial Signoff
            sb.AppendLine("<div style=\"font-size: 13px; color: #64748b; line-height: 1.5; margin-top: 28px; padding-top: 16px; border-top: 1px solid #e2e8f0;\">");
            sb.AppendLine("That's your briefing for this morning. We'll be back tomorrow with your next daily dispatch.<br>");
            sb.AppendLine("<strong>— The NewsBase Editorial Desk</strong>");
            sb.AppendLine("</div>");

            sb.AppendLine("</td></tr>");

            // Email Footer (Unsubscribe & Privacy)
            sb.AppendLine("<tr><td style=\"background-color: #f8fafc; padding: 22px 28px; border-top: 1px solid #e2e8f0; text-align: center; font-size: 11.5px; color: #64748b; line-height: 1.6;\">");
            sb.AppendLine("You are receiving this morning briefing because you subscribed to <strong>The Morning Dispatch</strong> on NewsBase.<br>");
            sb.AppendLine("<a href=\"{{UNSUBSCRIBE_URL}}\" style=\"color: #991b1b; text-decoration: underline; font-weight: 600;\">Unsubscribe at any time</a> &bull; Privacy protected &bull; Verified sources & wire dispatches<br>");
            sb.AppendLine("&copy; NewsBase. All rights reserved.");
            sb.AppendLine("</td></tr>");

            sb.AppendLine("</table>"); // End container
            sb.AppendLine("</td></tr></table>"); // End outer
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        // ==========================================
        // 5. PLAIN TEXT GENERATOR
        // ==========================================

        private static string GeneratePlainTextTemplate(CuratedNewsletterResult data, string baseUrl)
        {
            var sb = new StringBuilder();

            sb.AppendLine("NEWSBASE: THE MORNING DISPATCH");
            sb.AppendLine($"{data.FormattedDate} | Morning Edition (Asia/Kolkata)");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();
            sb.AppendLine(data.Introduction);
            sb.AppendLine();

            if (data.TopHeadlines.Count > 0)
            {
                sb.AppendLine("TOP STORIES OF THE DAY");
                sb.AppendLine(new string('-', 30));
                foreach (var s in data.TopHeadlines)
                {
                    sb.AppendLine($"* [{s.Category}] {s.Title}");
                    sb.AppendLine($"  {s.Summary}");
                    sb.AppendLine($"  Source: {s.Source} | {s.Url}");
                    sb.AppendLine();
                }
            }

            if (data.CategorizedNews.Count > 0)
            {
                sb.AppendLine("YESTERDAY'S TOP REPORTING BY CATEGORY");
                sb.AppendLine(new string('-', 30));
                foreach (var g in data.CategorizedNews)
                {
                    sb.AppendLine($"\n--- {g.CategoryName.ToUpperInvariant()} ---");
                    foreach (var s in g.Stories)
                    {
                        sb.AppendLine($"* {s.Title}");
                        sb.AppendLine($"  {s.Summary}");
                        sb.AppendLine($"  Source: {s.Source} ({s.Url})");
                    }
                }
                sb.AppendLine();
            }

            if (data.TodayEvents.Count > 0)
            {
                sb.AppendLine("IMPORTANT EVENTS HAPPENING TODAY");
                sb.AppendLine(new string('-', 30));
                foreach (var e in data.TodayEvents)
                {
                    sb.AppendLine($"* {e.EventName}");
                    sb.AppendLine($"  {e.Description}");
                    if (!string.IsNullOrWhiteSpace(e.WhyItMatters))
                    {
                        sb.AppendLine($"  Why it matters: {e.WhyItMatters}");
                    }
                    sb.AppendLine($"  Source: {e.Source} ({e.Url})");
                    sb.AppendLine();
                }
            }

            sb.AppendLine(new string('=', 50));
            sb.AppendLine("That's your briefing for this morning. We'll be back tomorrow.");
            sb.AppendLine("— The NewsBase Editorial Desk");
            sb.AppendLine();
            sb.AppendLine("Unsubscribe at any time: {{UNSUBSCRIBE_URL}}");
            sb.AppendLine("Privacy protected • (c) NewsBase");

            return sb.ToString();
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }

        private static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(s);
        }

        private static TimeZoneInfo GetIndiaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST");
                }
            }
        }

        private class CandidateStory
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string? Published { get; set; }
        }

        private class CandidateEvent
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }

        private class GeminiCuratedNewsletterDto
        {
            public string? Subject { get; set; }
            public string? Introduction { get; set; }
            public List<CuratedStoryDto>? TopHeadlines { get; set; }
            public List<CuratedCategoryGroupDto>? CategorizedNews { get; set; }
            public List<CuratedEventDto>? TodayEvents { get; set; }
        }
    }
}
