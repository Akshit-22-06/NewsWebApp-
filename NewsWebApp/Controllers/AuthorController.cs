using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Utils;
using NewsWebApp.ViewModels;
using NewsWebApp.Services;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Controller for viewing journalist profiles, and dedicated Author article submission and dashboard.
    /// </summary>
    public class AuthorController : Controller
    {
        private readonly IAuthorRepository _authorRepository;
        private readonly INewsRepository _newsRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGeminiModerationService _geminiModerationService;

        public AuthorController(
            IAuthorRepository authorRepository,
            INewsRepository newsRepository,
            ICategoryRepository categoryRepository,
            UserManager<ApplicationUser> userManager,
            IGeminiModerationService geminiModerationService)
        {
            _authorRepository = authorRepository;
            _newsRepository = newsRepository;
            _categoryRepository = categoryRepository;
            _userManager = userManager;
            _geminiModerationService = geminiModerationService;
        }

        // ==========================================
        // PUBLIC AUTHOR DIRECTORY
        // ==========================================

        // GET: /author
        public async Task<IActionResult> Index()
        {
            var authors = await _authorRepository.GetAllAsync();
            return View(authors);
        }

        // GET: /author/details/5
        public async Task<IActionResult> Details(int id)
        {
            var author = await _authorRepository.GetByIdAsync(id);
            if (author == null)
            {
                return NotFound();
            }

            return View(author);
        }

        // ==========================================
        // DEDICATED AUTHOR DASHBOARD & WORKFLOW
        // ==========================================

        // GET: /author/dashboard
        [Authorize(Roles = "Author,Admin")]
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;
            var author = await EnsureAuthorExistsAsync(userId);
            var articles = (await _newsRepository.GetByAuthorUserIdAsync(userId)).ToList();

            var viewModel = new AuthorDashboardViewModel
            {
                Author = author,
                Articles = articles,
                PendingCount = articles.Count(a => a.Status == ArticleStatus.Pending),
                ApprovedCount = articles.Count(a => a.Status == ArticleStatus.Approved),
                RejectedCount = articles.Count(a => a.Status == ArticleStatus.Rejected)
            };

            return View(viewModel);
        }

        // GET: /author/create
        [HttpGet]
        [Authorize(Roles = "Author,Admin")]
        public async Task<IActionResult> Create()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var model = new CreateAuthorArticleViewModel
            {
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            };
            return View(model);
        }

        // POST: /author/create
        [HttpPost]
        [Authorize(Roles = "Author,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAuthorArticleViewModel model)
        {
            // 1. Validate form fields
            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepository.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model);
            }

            // 2. Automated Gemini AI moderation check
            var moderation = await _geminiModerationService.ModerateArticleAsync(
                model.Title, model.Summary, model.Content);

            // 3. Fail-safe on service outage (Never auto-publish without review)
            if (!moderation.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, "We could not review your article right now. Please try again later.");
                var categories = await _categoryRepository.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model); // Preserves author's entered content!
            }

            var userId = _userManager.GetUserId(User)!;
            var author = await EnsureAuthorExistsAsync(userId);

            string slug = await SlugHelper.GenerateUniqueSlugAsync(
                model.Title,
                s => _newsRepository.SlugExistsAsync(s));

            if (moderation.Decision == "APPROVE")
            {
                // Case A: Article approved by Gemini -> Automatically approve and publish immediately
                var article = new NewsArticle
                {
                    Title = model.Title.Trim(),
                    Slug = slug,
                    Summary = model.Summary.Trim(),
                    Content = model.Content.Trim(),
                    ImageUrl = model.ImageUrl?.Trim(),
                    CategoryId = model.CategoryId,
                    AuthorId = author.Id,
                    Status = ArticleStatus.Approved,
                    IsPublished = true,
                    IsFeatured = false,
                    PublishedDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    RejectionReason = null,
                    ModerationQualityScore = moderation.QualityScore,
                    ModerationCategories = string.Join(", ", moderation.Categories),
                    ModerationModel = moderation.ModelVersion,
                    ModeratedAt = moderation.ModeratedAt
                };

                await _newsRepository.AddAsync(article);

                TempData["SuccessMessage"] = "Your article has passed our AI content review and has been approved successfully. It is now published.";
                return RedirectToAction(nameof(Dashboard));
            }
            else
            {
                // Case B: Article rejected by Gemini -> Do NOT publish, do NOT show in public articles
                var article = new NewsArticle
                {
                    Title = model.Title.Trim(),
                    Slug = slug,
                    Summary = model.Summary.Trim(),
                    Content = model.Content.Trim(),
                    ImageUrl = model.ImageUrl?.Trim(),
                    CategoryId = model.CategoryId,
                    AuthorId = author.Id,
                    Status = ArticleStatus.Rejected,
                    IsPublished = false,
                    PublishedDate = null,
                    RejectionReason = moderation.AuthorMessage,
                    CreatedAt = DateTime.UtcNow,
                    ModerationQualityScore = moderation.QualityScore,
                    ModerationCategories = string.Join(", ", moderation.Categories),
                    ModerationModel = moderation.ModelVersion,
                    ModeratedAt = moderation.ModeratedAt
                };

                await _newsRepository.AddAsync(article);

                TempData["ErrorMessage"] = $"Your article could not be approved: {moderation.AuthorMessage}";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // GET: /author/edit/5
        [HttpGet]
        [Authorize(Roles = "Author,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var article = await _newsRepository.GetByIdAsync(id);
            if (article == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (article.Author?.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var categories = await _categoryRepository.GetAllAsync();
            var model = new EditAuthorArticleViewModel
            {
                Id = article.Id,
                Title = article.Title,
                Summary = article.Summary,
                Content = article.Content,
                ImageUrl = article.ImageUrl,
                CategoryId = article.CategoryId,
                Status = article.Status,
                RejectionReason = article.RejectionReason,
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == article.CategoryId))
            };

            return View(model);
        }

        // POST: /author/edit/5
        [HttpPost]
        [Authorize(Roles = "Author,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditAuthorArticleViewModel model)
        {
            if (id != model.Id) return BadRequest();

            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepository.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model);
            }

            var article = await _newsRepository.GetByIdAsync(id);
            if (article == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (article.Author?.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Automated Gemini AI moderation check for edited content
            var moderation = await _geminiModerationService.ModerateArticleAsync(
                model.Title, model.Summary, model.Content);

            if (!moderation.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, "We could not review your article right now. Please try again later.");
                var categories = await _categoryRepository.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == model.CategoryId));
                return View(model);
            }

            // Update content and quality metadata
            article.Title = model.Title.Trim();
            article.Summary = model.Summary.Trim();
            article.Content = model.Content.Trim();
            article.ImageUrl = model.ImageUrl?.Trim();
            article.CategoryId = model.CategoryId;
            article.UpdatedDate = DateTime.UtcNow;
            article.ModerationQualityScore = moderation.QualityScore;
            article.ModerationCategories = string.Join(", ", moderation.Categories);
            article.ModerationModel = moderation.ModelVersion;
            article.ModeratedAt = moderation.ModeratedAt;

            if (moderation.Decision == "APPROVE")
            {
                article.Status = ArticleStatus.Approved;
                article.IsPublished = true;
                article.PublishedDate = DateTime.UtcNow;
                article.RejectionReason = null;

                await _newsRepository.UpdateAsync(article);

                TempData["SuccessMessage"] = "Your updated article has passed our AI content review and has been published successfully.";
                return RedirectToAction(nameof(Dashboard));
            }
            else
            {
                article.Status = ArticleStatus.Rejected;
                article.IsPublished = false;
                article.RejectionReason = moderation.AuthorMessage;

                await _newsRepository.UpdateAsync(article);

                TempData["ErrorMessage"] = $"Your updated article could not be approved: {moderation.AuthorMessage}";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // POST: /author/delete/5
        [HttpPost]
        [Authorize(Roles = "Author,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _newsRepository.GetByIdAsync(id);
            if (article == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (article.Author?.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await _newsRepository.DeleteAsync(id);
            TempData["SuccessMessage"] = "Article deleted successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private async Task<Author> EnsureAuthorExistsAsync(string userId)
        {
            var author = await _authorRepository.GetByUserIdAsync(userId);
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
                await _authorRepository.AddAsync(author);
            }
            return author;
        }
    }
}
