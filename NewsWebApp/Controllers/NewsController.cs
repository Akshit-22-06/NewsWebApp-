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
    /// Simple and clean controller for browsing, searching, reading news, bookmarks, likes, comments, and city-area live news.
    /// </summary>
    public class NewsController : Controller
    {
        private readonly INewsRepository _newsRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ICommentRepository _commentRepo;
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILiveNewsService _liveNewsService;

        public NewsController(
            INewsRepository newsRepo,
            ICategoryRepository categoryRepo,
            ICommentRepository commentRepo,
            IBookmarkRepository bookmarkRepo,
            UserManager<ApplicationUser> userManager,
            ILiveNewsService liveNewsService)
        {
            _newsRepo = newsRepo;
            _categoryRepo = categoryRepo;
            _commentRepo = commentRepo;
            _bookmarkRepo = bookmarkRepo;
            _userManager = userManager;
            _liveNewsService = liveNewsService;
        }

        // GET: /news
        public async Task<IActionResult> Index(
            string? search, 
            int? categoryId, 
            string? sortBy, 
            string? sourceType, 
            string? region, 
            string? locationScope, 
            int page = 1)
        {
            const int pageSize = 9;
            var (articles, totalCount) = await _newsRepo.GetPagedPublishedAsync(
                search, categoryId, sortBy, page, pageSize, sourceType, null, region, locationScope);

            string? categoryName = null;
            if (categoryId.HasValue)
            {
                var cat = await _categoryRepo.GetByIdAsync(categoryId.Value);
                categoryName = cat?.Name;
            }

            var availableRegions = await _newsRepo.GetAvailableRegionsAsync();

            var model = new NewsListViewModel
            {
                Articles = articles,
                CurrentPage = page,
                PageSize = pageSize,
                TotalArticles = totalCount,
                SearchTerm = search,
                CategoryId = categoryId,
                CategoryName = categoryName,
                SortBy = sortBy,
                SourceType = sourceType,
                Region = region,
                LocationScope = locationScope,
                AvailableRegions = availableRegions,
                Categories = await _categoryRepo.GetAllAsync()
            };

            return View(model);
        }

        // GET: /news/category/{slug}
        public async Task<IActionResult> Category(string slug, string? sortBy, int page = 1)
        {
            var category = await _categoryRepo.GetBySlugAsync(slug);
            if (category == null) return NotFound();

            return await Index(null, category.Id, sortBy, null, null, null, page);
        }

        // GET: /news/details/{slug}
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return NotFound();

            var article = await _newsRepo.GetBySlugAsync(slug);
            if (article == null || !article.IsPublished) return NotFound();

            await _newsRepo.IncrementViewCountAsync(article.Id);

            var related = await _newsRepo.GetRelatedAsync(article.Id, article.CategoryId, 3);
            var userId = _userManager.GetUserId(User);
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

        // GET: /news/city?city=Pune
        public async Task<IActionResult> City(string? city)
        {
            var model = new CityNewsViewModel
            {
                City = city?.Trim()
            };

            if (!string.IsNullOrWhiteSpace(city))
            {
                string cleanCity = city.Trim();
                if (cleanCity.Length > 1)
                {
                    cleanCity = char.ToUpper(cleanCity[0]) + cleanCity[1..].ToLower();
                }
                model.City = cleanCity;

                // 1. Check existing articles in database
                var existing = (await _newsRepo.GetCityArticlesAsync(cleanCity, 30)).ToList();

                // 2. If fewer than 3 articles or no articles from the past 48 hours, fetch live breaking news on-demand
                bool hasRecent = existing.Any(a => a.PublishedDate.HasValue && a.PublishedDate.Value > DateTime.UtcNow.AddDays(-2));
                if (existing.Count < 3 || !hasRecent)
                {
                    var syncResult = await _liveNewsService.FetchAndSyncCityNewsAsync(cleanCity);
                    if (syncResult.NewArticlesAdded > 0)
                    {
                        model.IsLiveFetched = true;
                        existing = (await _newsRepo.GetCityArticlesAsync(cleanCity, 30)).ToList();
                    }
                }

                model.Articles = existing;
                model.TotalFound = existing.Count;
            }

            return View(model);
        }

        // POST: /news/refreshcity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshCity(string city)
        {
            if (!string.IsNullOrWhiteSpace(city))
            {
                var result = await _liveNewsService.FetchAndSyncCityNewsAsync(city);
                TempData["SuccessMessage"] = $"Fetched {result.NewArticlesAdded} live news headlines for {city}!";
            }
            return RedirectToAction(nameof(City), new { city });
        }
    }
}
