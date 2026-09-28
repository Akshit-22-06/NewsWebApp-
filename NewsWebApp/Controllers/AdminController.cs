using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Services;
using NewsWebApp.Utils;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Clean, beginner-friendly Admin controller for managing articles, categories, comments, and live feeds.
    /// Accessible to users with "Admin" or "Author" roles.
    /// </summary>
    [Authorize(Roles = "Admin,Author")]
    public class AdminController : Controller
    {
        private readonly INewsRepository _newsRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ICommentRepository _commentRepo;
        private readonly IAuthorRepository _authorRepo;
        private readonly INewsFeedSourceRepository _feedRepo;
        private readonly INewsletterRepository _newsletterRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILiveNewsService _liveNewsService;

        public AdminController(
            INewsRepository newsRepo,
            ICategoryRepository categoryRepo,
            ICommentRepository commentRepo,
            IAuthorRepository authorRepo,
            INewsFeedSourceRepository feedRepo,
            INewsletterRepository newsletterRepo,
            UserManager<ApplicationUser> userManager,
            ILiveNewsService liveNewsService)
        {
            _newsRepo = newsRepo;
            _categoryRepo = categoryRepo;
            _commentRepo = commentRepo;
            _authorRepo = authorRepo;
            _feedRepo = feedRepo;
            _newsletterRepo = newsletterRepo;
            _userManager = userManager;
            _liveNewsService = liveNewsService;
        }

        // ==========================================
        // 1. DASHBOARD
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                TotalArticles = await _newsRepo.GetTotalCountAsync(),
                PublishedArticles = await _newsRepo.GetPublishedCountAsync(),
                DraftArticles = await _newsRepo.GetDraftCountAsync(),
                TotalViews = await _newsRepo.GetTotalViewsAsync(),
                TotalCategories = await _categoryRepo.GetTotalCountAsync(),
                TotalComments = await _commentRepo.GetTotalCountAsync(),
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalSubscribers = await _newsletterRepo.GetSubscriberCountAsync(),
                RecentArticles = await _newsRepo.GetLatestAsync(5),
                RecentComments = await _commentRepo.GetRecentAsync(5)
            };
            return View(model);
        }

        // ==========================================
        // 2. ARTICLE MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Articles(string? search, int? categoryId, bool? isPublished, int page = 1)
        {
            const int pageSize = 10;
            string? authorFilter = User.IsInRole("Admin") ? null : _userManager.GetUserId(User);

            var (articles, totalCount) = await _newsRepo.GetPagedAdminAsync(
                search, categoryId, isPublished, page, pageSize, authorFilter);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.IsPublished = isPublished;
            ViewBag.Categories = await _categoryRepo.GetAllAsync();

            return View(articles);
        }

        [HttpGet]
        public async Task<IActionResult> CreateArticle()
        {
            var categories = await _categoryRepo.GetAllAsync();
            return View(new CreateNewsViewModel
            {
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArticle(CreateNewsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepo.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var author = await EnsureAuthorExistsAsync(userId);

            string slug = await SlugHelper.GenerateUniqueSlugAsync(
                string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug,
                s => _newsRepo.SlugExistsAsync(s));

            var article = new NewsArticle
            {
                Title = model.Title.Trim(),
                Slug = slug,
                Summary = model.Summary.Trim(),
                Content = model.Content.Trim(),
                ImageUrl = model.ImageUrl?.Trim(),
                CategoryId = model.CategoryId,
                AuthorId = author.Id,
                IsPublished = model.IsPublished,
                IsFeatured = User.IsInRole("Admin") && model.IsFeatured,
                PublishedDate = model.IsPublished ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };

            await _newsRepo.AddAsync(article);
            TempData["SuccessMessage"] = "Article created successfully!";
            return RedirectToAction(nameof(Articles));
        }

        [HttpGet]
        public async Task<IActionResult> EditArticle(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();
            if (!CanManageArticle(article)) return Forbid();

            var categories = await _categoryRepo.GetAllAsync();
            var model = new EditNewsViewModel
            {
                Id = article.Id,
                Title = article.Title,
                Slug = article.Slug,
                Summary = article.Summary,
                Content = article.Content,
                ImageUrl = article.ImageUrl,
                CategoryId = article.CategoryId,
                IsPublished = article.IsPublished,
                IsFeatured = article.IsFeatured,
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == article.CategoryId))
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditArticle(int id, EditNewsViewModel model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepo.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model);
            }

            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();
            if (!CanManageArticle(article)) return Forbid();

            article.Title = model.Title.Trim();
            article.Summary = model.Summary.Trim();
            article.Content = model.Content.Trim();
            article.ImageUrl = model.ImageUrl?.Trim();
            article.CategoryId = model.CategoryId;
            article.IsPublished = model.IsPublished;
            if (User.IsInRole("Admin")) article.IsFeatured = model.IsFeatured;
            if (model.IsPublished && article.PublishedDate == null) article.PublishedDate = DateTime.UtcNow;

            await _newsRepo.UpdateAsync(article);
            TempData["SuccessMessage"] = "Article updated successfully!";
            return RedirectToAction(nameof(Articles));
        }

        [HttpGet]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();
            if (!CanManageArticle(article)) return Forbid();
            return View(article);
        }

        [HttpPost, ActionName("DeleteArticle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArticleConfirmed(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();
            if (!CanManageArticle(article)) return Forbid();

            await _newsRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Article deleted successfully!";
            return RedirectToAction(nameof(Articles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();
            if (!CanManageArticle(article)) return Forbid();

            article.IsPublished = !article.IsPublished;
            if (article.IsPublished && article.PublishedDate == null) article.PublishedDate = DateTime.UtcNow;
            await _newsRepo.UpdateAsync(article);

            TempData["SuccessMessage"] = $"Publish status updated for \"{article.Title}\".";
            return RedirectToAction(nameof(Articles));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeatured(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();

            article.IsFeatured = !article.IsFeatured;
            await _newsRepo.UpdateAsync(article);
            TempData["SuccessMessage"] = $"Featured status updated for \"{article.Title}\".";
            return RedirectToAction(nameof(Articles));
        }

        // ==========================================
        // 3. CATEGORY MANAGEMENT (ADMIN ONLY)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Categories() => View(await _categoryRepo.GetCategoriesWithCountAsync());

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateCategory() => View(new CategoryViewModel());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            string slug = await SlugHelper.GenerateUniqueSlugAsync(
                string.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug,
                s => _categoryRepo.SlugExistsAsync(s));

            await _categoryRepo.AddAsync(new Category { Name = model.Name.Trim(), Slug = slug, Description = model.Description?.Trim() });
            TempData["SuccessMessage"] = "Category created successfully!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null) return NotFound();
            return View(new CategoryViewModel { Id = cat.Id, Name = cat.Name, Slug = cat.Slug, Description = cat.Description });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, CategoryViewModel model)
        {
            if (id != model.Id || !ModelState.IsValid) return View(model);
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null) return NotFound();

            cat.Name = model.Name.Trim();
            cat.Description = model.Description?.Trim();
            await _categoryRepo.UpdateAsync(cat);

            TempData["SuccessMessage"] = "Category updated successfully!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _categoryRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Category deleted successfully!";
            return RedirectToAction(nameof(Categories));
        }

        // ==========================================
        // 4. COMMENTS, USERS & SUBSCRIBERS (ADMIN ONLY)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Comments() => View(await _commentRepo.GetAllAsync());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            await _commentRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Comment deleted.";
            return RedirectToAction(nameof(Comments));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var list = new List<(ApplicationUser User, IList<string> Roles)>();
            foreach (var user in users)
            {
                list.Add((user, await _userManager.GetRolesAsync(user)));
            }
            return View(list);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Subscribers() => View(await _newsletterRepo.GetAllSubscribersAsync());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubscriber(int id)
        {
            await _newsletterRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Subscriber removed.";
            return RedirectToAction(nameof(Subscribers));
        }

        // ==========================================
        // 5. LIVE NEWS HUB (ADMIN ONLY)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LiveNews()
        {
            var (articles, _) = await _newsRepo.GetPagedAdminAsync(null, null, null, 1, 10);
            var model = new LiveNewsHubViewModel
            {
                FeedSources = _liveNewsService.GetConfiguredFeedSources(),
                CustomFeedSources = await _feedRepo.GetAllAsync(),
                TotalLiveArticles = await _newsRepo.GetLiveSyncedCountAsync(),
                TotalArticles = await _newsRepo.GetTotalCountAsync(),
                IsAutoSyncEnabled = true,
                SyncIntervalMinutes = 60,
                AvailableCategories = await _categoryRepo.GetAllAsync(),
                RecentLiveArticles = articles.Where(a => a.IsLiveSynced).Take(6)
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncLiveNews()
        {
            var result = await _liveNewsService.SyncAllFeedsAsync();
            TempData["SuccessMessage"] = $"Sync complete! Added {result.NewArticlesAdded} new articles ({result.DuplicatesSkipped} duplicates skipped).";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncHackerNews()
        {
            var result = await _liveNewsService.SyncHackerNewsAsync();
            TempData["SuccessMessage"] = $"Hacker News sync complete! Added {result.NewArticlesAdded} articles.";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncSpecificFeed(string feedUrl, string categorySlug, string sourceName)
        {
            var result = await _liveNewsService.SyncFeedAsync(feedUrl, categorySlug, sourceName);
            TempData["SuccessMessage"] = $"Fetched {result.NewArticlesAdded} articles from {sourceName}.";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncCustomFeed(int id)
        {
            var result = await _liveNewsService.SyncCustomSourceAsync(id);
            TempData["SuccessMessage"] = $"Custom feed synced! Added {result.NewArticlesAdded} articles.";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFeedSource(string name, string feedUrl, string categorySlug, string? defaultImageUrl)
        {
            await _feedRepo.AddAsync(new NewsFeedSource
            {
                Name = name.Trim(),
                FeedUrl = feedUrl.Trim(),
                CategorySlug = categorySlug.Trim().ToLower(),
                DefaultImageUrl = defaultImageUrl?.Trim() ?? "https://images.unsplash.com/photo-1504711434969-e33886168f5c?w=800",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            TempData["SuccessMessage"] = $"Feed source '{name}' added successfully!";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeedSource(int id)
        {
            bool active = await _feedRepo.ToggleActiveAsync(id);
            TempData["SuccessMessage"] = active ? "Feed activated." : "Feed paused.";
            return RedirectToAction(nameof(LiveNews));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFeedSource(int id)
        {
            await _feedRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Feed source deleted.";
            return RedirectToAction(nameof(LiveNews));
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private bool CanManageArticle(NewsArticle article)
        {
            if (User.IsInRole("Admin")) return true;
            var currentUserId = _userManager.GetUserId(User);
            return article.Author?.UserId == currentUserId;
        }

        private async Task<Author> EnsureAuthorExistsAsync(string userId)
        {
            var author = await _authorRepo.GetByUserIdAsync(userId);
            if (author == null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                author = new Author
                {
                    UserId = userId,
                    DisplayName = user?.FullName ?? user?.UserName ?? "Journalist",
                    Bio = user?.Bio,
                    ProfileImageUrl = user?.ProfilePictureUrl
                };
                await _authorRepo.AddAsync(author);
            }
            return author;
        }
    }
}
