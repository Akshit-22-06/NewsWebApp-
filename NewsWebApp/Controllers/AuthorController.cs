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

        public AuthorController(
            IAuthorRepository authorRepository,
            INewsRepository newsRepository,
            ICategoryRepository categoryRepository,
            UserManager<ApplicationUser> userManager)
        {
            _authorRepository = authorRepository;
            _newsRepository = newsRepository;
            _categoryRepository = categoryRepository;
            _userManager = userManager;
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
            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepository.GetAllAsync();
                model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var author = await EnsureAuthorExistsAsync(userId);

            string slug = await SlugHelper.GenerateUniqueSlugAsync(
                model.Title,
                s => _newsRepository.SlugExistsAsync(s));

            // Newly created Author articles always start as Pending review, NOT published
            var article = new NewsArticle
            {
                Title = model.Title.Trim(),
                Slug = slug,
                Summary = model.Summary.Trim(),
                Content = model.Content.Trim(),
                ImageUrl = model.ImageUrl?.Trim(),
                CategoryId = model.CategoryId,
                AuthorId = author.Id,
                Status = ArticleStatus.Pending,
                IsPublished = false,
                IsFeatured = false,
                RejectionReason = null,
                CreatedAt = DateTime.UtcNow,
                PublishedDate = null
            };

            await _newsRepository.AddAsync(article);

            TempData["SuccessMessage"] = "Your article has been submitted for review! An administrator will review and approve it before it becomes publicly visible.";
            return RedirectToAction(nameof(Dashboard));
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

            // Update content and reset status to Pending for review
            article.Title = model.Title.Trim();
            article.Summary = model.Summary.Trim();
            article.Content = model.Content.Trim();
            article.ImageUrl = model.ImageUrl?.Trim();
            article.CategoryId = model.CategoryId;
            article.Status = ArticleStatus.Pending;
            article.RejectionReason = null;
            article.IsPublished = false;

            await _newsRepository.UpdateAsync(article);

            TempData["SuccessMessage"] = "Article updated and resubmitted for admin review.";
            return RedirectToAction(nameof(Dashboard));
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
