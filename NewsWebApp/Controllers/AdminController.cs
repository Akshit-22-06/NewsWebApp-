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
    /// Clean, beginner-friendly Admin controller for managing articles, review moderation, categories, comments, users, and live news.
    /// Accessible exclusively to users with the "Admin" role.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly INewsRepository _newsRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ICommentRepository _commentRepo;
        private readonly IAuthorRepository _authorRepo;
        private readonly INewsletterRepository _newsletterRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INewsService _newsService;
        private readonly IConfiguration _configuration;

        public AdminController(
            INewsRepository newsRepo,
            ICategoryRepository categoryRepo,
            ICommentRepository commentRepo,
            IAuthorRepository authorRepo,
            INewsletterRepository newsletterRepo,
            UserManager<ApplicationUser> userManager,
            INewsService newsService,
            IConfiguration configuration)
        {
            _newsRepo = newsRepo;
            _categoryRepo = categoryRepo;
            _commentRepo = commentRepo;
            _authorRepo = authorRepo;
            _newsletterRepo = newsletterRepo;
            _userManager = userManager;
            _newsService = newsService;
            _configuration = configuration;
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

            ViewBag.PendingCount = await _newsRepo.GetPendingCountAsync();
            return View(model);
        }

        // ==========================================
        // 2. ARTICLE MANAGEMENT & MODERATION
        // ==========================================
        public async Task<IActionResult> Articles(string? search, int? categoryId, bool? isPublished, string? status, int page = 1)
        {
            const int pageSize = 10;

            var (articles, totalCount) = await _newsRepo.GetPagedAdminAsync(
                search, categoryId, isPublished, page, pageSize, authorUserId: null, status: status);

            var pendingArticles = await _newsRepo.GetPendingAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.IsPublished = isPublished;
            ViewBag.Status = status;
            ViewBag.Categories = await _categoryRepo.GetAllAsync();
            ViewBag.PendingArticles = pendingArticles;

            return View(articles);
        }

        // GET: /Admin/ReviewArticle/5
        [HttpGet]
        public async Task<IActionResult> ReviewArticle(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();

            return View(article);
        }

        // POST: /Admin/ApproveArticle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveArticle(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();

            article.Status = ArticleStatus.Approved;
            article.IsPublished = true;
            article.RejectionReason = null;
            if (article.PublishedDate == null)
            {
                article.PublishedDate = DateTime.UtcNow;
            }

            await _newsRepo.UpdateAsync(article);
            TempData["SuccessMessage"] = $"Article \"{article.Title}\" has been approved and published to the website!";
            return RedirectToAction(nameof(Articles));
        }

        // POST: /Admin/RejectArticle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectArticle(int id, string? rejectionReason)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();

            article.Status = ArticleStatus.Rejected;
            article.IsPublished = false;
            article.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                ? "This article does not meet our editorial guidelines."
                : rejectionReason.Trim();

            await _newsRepo.UpdateAsync(article);
            TempData["SuccessMessage"] = $"Article \"{article.Title}\" has been rejected with feedback.";
            return RedirectToAction(nameof(Articles));
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
                Status = ArticleStatus.Approved, // Staff/admin created articles are approved by default
                IsPublished = model.IsPublished,
                IsFeatured = model.IsFeatured,
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

            article.Title = model.Title.Trim();
            article.Summary = model.Summary.Trim();
            article.Content = model.Content.Trim();
            article.ImageUrl = model.ImageUrl?.Trim();
            article.CategoryId = model.CategoryId;
            article.IsPublished = model.IsPublished;
            article.IsFeatured = model.IsFeatured;
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
            return View(article);
        }

        [HttpPost, ActionName("DeleteArticle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArticleConfirmed(int id)
        {
            var article = await _newsRepo.GetByIdAsync(id);
            if (article == null) return NotFound();

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

            article.IsPublished = !article.IsPublished;
            if (article.IsPublished)
            {
                article.Status = ArticleStatus.Approved;
                if (article.PublishedDate == null) article.PublishedDate = DateTime.UtcNow;
            }
            await _newsRepo.UpdateAsync(article);

            TempData["SuccessMessage"] = $"Publish status updated for \"{article.Title}\".";
            return RedirectToAction(nameof(Articles));
        }

        [HttpPost]
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
        // 3. CATEGORY MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Categories() => View(await _categoryRepo.GetCategoriesWithCountAsync());

        [HttpGet]
        public IActionResult CreateCategory() => View(new CategoryViewModel());

        [HttpPost]
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
        public async Task<IActionResult> EditCategory(int id)
        {
            var cat = await _categoryRepo.GetByIdAsync(id);
            if (cat == null) return NotFound();
            return View(new CategoryViewModel { Id = cat.Id, Name = cat.Name, Slug = cat.Slug, Description = cat.Description });
        }

        [HttpPost]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _categoryRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Category deleted successfully!";
            return RedirectToAction(nameof(Categories));
        }

        // ==========================================
        // 4. COMMENTS, USERS & SUBSCRIBERS
        // ==========================================
        public async Task<IActionResult> Comments() => View(await _commentRepo.GetAllAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            await _commentRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Comment deleted.";
            return RedirectToAction(nameof(Comments));
        }

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

        public async Task<IActionResult> Subscribers() => View(await _newsletterRepo.GetAllSubscribersAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubscriber(int id)
        {
            await _newsletterRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Subscriber removed.";
            return RedirectToAction(nameof(Subscribers));
        }

        // ==========================================
        // 5. CURRENTS API - LIVE NEWS
        // ==========================================
        public async Task<IActionResult> LiveNews(string? category, string? search)
        {
            var apiKey = _configuration["CurrentsApi:ApiKey"];
            var baseUrl = _configuration["CurrentsApi:BaseUrl"] ?? "https://api.currentsapi.services/";

            var model = new LiveNewsHubViewModel
            {
                IsApiKeyConfigured = !string.IsNullOrWhiteSpace(apiKey),
                BaseUrl = baseUrl,
                SelectedCategory = category,
                SearchQuery = search
            };

            if (model.IsApiKeyConfigured)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        model.Articles = await _newsService.SearchNewsAsync(search.Trim(), category);
                    }
                    else
                    {
                        model.Articles = await _newsService.GetLatestNewsAsync(category);
                    }
                }
                catch (Exception ex)
                {
                    model.ErrorMessage = $"Failed to fetch news from Currents API: {ex.Message}";
                }
            }

            return View(model);
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private async Task<Author> EnsureAuthorExistsAsync(string userId)
        {
            var author = await _authorRepo.GetByUserIdAsync(userId);
            if (author == null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                author = new Author
                {
                    UserId = userId,
                    DisplayName = user?.FullName ?? user?.UserName ?? "Staff Editor",
                    Bio = user?.Bio,
                    ProfileImageUrl = user?.ProfilePictureUrl
                };
                await _authorRepo.AddAsync(author);
            }
            return author;
        }
    }
}
