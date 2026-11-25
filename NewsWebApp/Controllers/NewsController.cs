using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Services;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Simple and clean controller for browsing, searching Currents API news, reading articles, bookmarks, likes, and comments.
    /// </summary>
    public class NewsController : Controller
    {
        private readonly INewsService _newsService;
        private readonly INewsRepository _newsRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ICommentRepository _commentRepo;
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NewsController> _logger;

        public NewsController(
            INewsService newsService,
            INewsRepository newsRepo,
            ICategoryRepository categoryRepo,
            ICommentRepository commentRepo,
            IBookmarkRepository bookmarkRepo,
            UserManager<ApplicationUser> userManager,
            ILogger<NewsController> logger)
        {
            _newsService = newsService;
            _newsRepo = newsRepo;
            _categoryRepo = categoryRepo;
            _commentRepo = commentRepo;
            _bookmarkRepo = bookmarkRepo;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /news
        public async Task<IActionResult> Index(
            string? search, 
            string? category, 
            string? country, 
            string? language,
            string? source = "all")
        {
            var categories = await _categoryRepo.GetAllAsync();
            int? matchedCategoryId = null;

            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = categories.FirstOrDefault(c => 
                    c.Slug.Equals(category, StringComparison.OrdinalIgnoreCase) || 
                    c.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
                matchedCategoryId = cat?.Id;
            }

            // 1. Fetch NewsBase local published articles
            var (localArticles, _) = await _newsRepo.GetPagedAdminAsync(
                search: search,
                categoryId: matchedCategoryId,
                isPublished: true,
                page: 1,
                pageSize: 30,
                status: ArticleStatus.Approved);

            // 2. Fetch Currents API live wire news
            List<NewsDto> liveNews = new();
            string? errorMessage = null;

            if (source != "editorial")
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        liveNews = await _newsService.SearchNewsAsync(search.Trim(), category, country, language);
                    }
                    else if (!string.IsNullOrWhiteSpace(category))
                    {
                        liveNews = await _newsService.GetNewsByCategoryAsync(category.Trim());
                    }
                    else
                    {
                        liveNews = await _newsService.GetLatestNewsAsync(category, country, language);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load live wire news from Currents API.");
                    errorMessage = "Global live wire is currently slow or unavailable. Showing NewsBase editorial stories.";
                }
            }

            var viewModel = new NewsIndexViewModel
            {
                LocalArticles = localArticles,
                LiveNews = liveNews,
                Categories = categories,
                SearchTerm = search,
                SelectedCategory = category,
                SelectedCountry = country,
                SelectedLanguage = language,
                SourceType = source ?? "all",
                ErrorMessage = errorMessage
            };

            return View("Index", viewModel);
        }

        // GET: /news/search or /search?q=technology
        public async Task<IActionResult> Search(
            string? query, 
            string? q, 
            string? category, 
            string? country, 
            string? language)
        {
            var searchTerm = !string.IsNullOrWhiteSpace(query) ? query : q;
            return await Index(searchTerm, category, country, language);
        }

        // GET: /news/category/{slug}
        public async Task<IActionResult> Category(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return RedirectToAction(nameof(Index));
            return await Index(null, slug, null, null);
        }

        // GET: /news/details/{slug}
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return NotFound();

            var article = await _newsRepo.GetBySlugAsync(slug);
            if (article == null || !article.IsPublished) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAuthorizedViewer = User.IsInRole("Admin") || 
                (article.Author != null && !string.IsNullOrEmpty(userId) && article.Author.UserId == userId);

            if (article.Status != ArticleStatus.Approved && !isAuthorizedViewer)
            {
                return NotFound();
            }

            // Minimal privacy-preserving viewer hash to throttle duplicate view spamming
            string? viewerHash = null;
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{ip}|{userAgent}"));
                viewerHash = Convert.ToHexString(hashBytes)[..16];
            }
            catch
            {
                // Non-blocking fallback
            }

            await _newsRepo.IncrementViewCountAsync(article.Id, viewerHash);

            var related = await _newsRepo.GetRelatedAsync(article.Id, article.CategoryId, 3);
            bool isBookmarked = !string.IsNullOrEmpty(userId) && await _bookmarkRepo.IsBookmarkedAsync(userId, article.Id);

            int wordCount = (article.Content ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            int readMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

            var model = new NewsDetailsViewModel
            {
                Article = article,
                RelatedArticles = related,
                IsBookmarked = isBookmarked,
                EstimatedReadMinutes = readMinutes,
                NewComment = new AddCommentViewModel { NewsArticleId = article.Id, ArticleSlug = article.Slug }
            };

            return View(model);
        }

        // POST: /news/like/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Like(int id, string? returnUrl, string? slug)
        {
            await _newsRepo.IncrementLikeCountAsync(id);
            TempData["SuccessMessage"] = "Thank you for liking this story!";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction(nameof(Details), new { slug });
        }

        // POST: /news/togglebookmark/{id}
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBookmark(int id, string? returnUrl, string? slug)
        {
            var userId = _userManager.GetUserId(User)!;
            bool bookmarked = await _bookmarkRepo.ToggleBookmarkAsync(userId, id);
            TempData["SuccessMessage"] = bookmarked ? "Saved to your reading list!" : "Removed from reading list.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            if (!string.IsNullOrEmpty(slug)) return RedirectToAction(nameof(Details), new { slug });
            return RedirectToAction(nameof(Bookmarks));
        }

        // GET: /news/bookmarks
        [Authorize]
        public async Task<IActionResult> Bookmarks()
        {
            var userId = _userManager.GetUserId(User)!;
            var articles = await _bookmarkRepo.GetUserBookmarksAsync(userId);
            return View(new BookmarkListViewModel
            {
                BookmarkedArticles = articles,
                TotalBookmarks = await _bookmarkRepo.GetUserBookmarkCountAsync(userId)
            });
        }

        // POST: /news/addcomment
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int newsArticleId, string articleSlug, string content)
        {
            if (newsArticleId <= 0 || string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Comment text cannot be empty.";
                return RedirectToAction(nameof(Details), new { slug = articleSlug });
            }

            var userId = _userManager.GetUserId(User)!;
            await _commentRepo.AddAsync(new Comment
            {
                NewsArticleId = newsArticleId,
                UserId = userId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            TempData["SuccessMessage"] = "Comment posted successfully!";
            return RedirectToAction(nameof(Details), new { slug = articleSlug });
        }

        // POST: /news/deletecomment
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id, string slug)
        {
            var comment = await _commentRepo.GetByIdAsync(id);
            if (comment != null)
            {
                var userId = _userManager.GetUserId(User);
                if (comment.UserId == userId || User.IsInRole("Admin"))
                {
                    await _commentRepo.DeleteAsync(id);
                    TempData["SuccessMessage"] = "Comment deleted.";
                }
            }
            return RedirectToAction(nameof(Details), new { slug });
        }
    }
}
