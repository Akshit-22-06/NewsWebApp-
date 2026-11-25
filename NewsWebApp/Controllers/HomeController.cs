using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.Services;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Handles public landing, live news feed, newsletter subscriptions, and general info pages.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly INewsRepository _newsRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly INewsletterRepository _newsletterRepository;
        private readonly INewsletterSchedulerManager _schedulerManager;
        private readonly INewsService _newsService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            INewsRepository newsRepository, 
            ICategoryRepository categoryRepository,
            INewsletterRepository newsletterRepository,
            INewsletterSchedulerManager schedulerManager,
            INewsService newsService,
            ILogger<HomeController> logger)
        {
            _newsRepository = newsRepository;
            _categoryRepository = categoryRepository;
            _newsletterRepository = newsletterRepository;
            _schedulerManager = schedulerManager;
            _newsService = newsService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<NewsDto> liveNews = new();
            try
            {
                liveNews = await _newsService.GetLatestNewsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load live news from Currents API on Home page.");
            }

            var featured = await _newsRepository.GetFeaturedAsync(3);
            var latest = await _newsRepository.GetLatestAsync(6);
            var popular = await _newsRepository.GetPopularAsync(5);

            List<TrendingArticleDto> trending = new();
            try
            {
                trending = await _newsService.GetTrendingNewsTodayAsync(5);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load live trending news from Currents API on Home page.");
            }
            var categoriesWithCount = await _categoryRepository.GetCategoriesWithCountAsync();

            var viewModel = new HomeViewModel
            {
                LiveNews = liveNews,
                FeaturedArticles = featured,
                LatestArticles = latest,
                PopularArticles = popular,
                TrendingArticles = trending,
                CategoriesWithCounts = categoriesWithCount
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email, string? returnUrl)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var (success, message) = await _schedulerManager.SubscribeAndSendWelcomeAsync(email, baseUrl);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success, message });
            }

            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult About()
        {
            ViewData["Title"] = "About Us";
            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy";
            return View();
        }

        public IActionResult Terms()
        {
            ViewData["Title"] = "Terms of Service";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            ViewBag.StatusCode = statusCode;
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return View(new ErrorViewModel { RequestId = requestId });
        }
    }
}
