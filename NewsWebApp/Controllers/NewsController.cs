using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Simple and clean controller for browsing, searching, reading news, bookmarks, likes, and comments.
    /// </summary>
    public class NewsController : Controller
    {
        private readonly INewsRepository _newsRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly ICommentRepository _commentRepo;
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly UserManager<ApplicationUser> _userManager;

        public NewsController(
            INewsRepository newsRepo,
            ICategoryRepository categoryRepo,
            ICommentRepository commentRepo,
            IBookmarkRepository bookmarkRepo,
            UserManager<ApplicationUser> userManager)
        {
            _newsRepo = newsRepo;
            _categoryRepo = categoryRepo;
            _commentRepo = commentRepo;
            _bookmarkRepo = bookmarkRepo;
            _userManager = userManager;
        }

        // GET: /news
        public async Task<IActionResult> Index(string? search, int? categoryId, string? sortBy, string? sourceType, int page = 1)
        {
            const int pageSize = 9;
            var (articles, totalCount) = await _newsRepo.GetPagedPublishedAsync(
                search, categoryId, sortBy, page, pageSize, sourceType);

            string? categoryName = null;
            if (categoryId.HasValue)
            {
                var cat = await _categoryRepo.GetByIdAsync(categoryId.Value);
                categoryName = cat?.Name;
            }

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
                Categories = await _categoryRepo.GetAllAsync()
            };

            return View(model);
        }

        // GET: /news/category/{slug}
        public async Task<IActionResult> Category(string slug, string? sortBy, int page = 1)
        {
            var category = await _categoryRepo.GetBySlugAsync(slug);
            if (category == null) return NotFound();

            return await Index(null, category.Id, sortBy, null, page);
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
    }
}
