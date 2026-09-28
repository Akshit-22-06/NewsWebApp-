using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;
using NewsWebApp.ViewModels;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Handles public landing, breaking news ticker, newsletter subscriptions, and general info pages.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly INewsRepository _newsRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly INewsletterRepository _newsletterRepository;

        public HomeController(
            INewsRepository newsRepository, 
            ICategoryRepository categoryRepository,
            INewsletterRepository newsletterRepository)
        {
            _newsRepository = newsRepository;
            _categoryRepository = categoryRepository;
            _newsletterRepository = newsletterRepository;
        }

        public async Task<IActionResult> Index()
        {
            var featured = await _newsRepository.GetFeaturedAsync(3);
            var latest = await _newsRepository.GetLatestAsync(8);
            var popular = await _newsRepository.GetPopularAsync(5);
            var discussed = await _newsRepository.GetMostDiscussedAsync(5);
            var breaking = await _newsRepository.GetLiveBreakingNewsAsync(8);
            var categoriesWithCount = await _categoryRepository.GetCategoriesWithCountAsync();

            var viewModel = new HomeViewModel
            {
                FeaturedArticles = featured,
                LatestArticles = latest,
                PopularArticles = popular,
                MostDiscussedArticles = discussed,
                BreakingNewsTicker = breaking,
                CategoriesWithCounts = categoriesWithCount
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email, string? returnUrl)
        {
            var (success, message) = await _newsletterRepository.SubscribeAsync(email);
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            ViewBag.StatusCode = statusCode;
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return View(new ErrorViewModel { RequestId = requestId });
        }
    }
}
