using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewsWebApp.Repositories.Interfaces;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Controller for browsing categories and exploring category-specific news feeds.
    /// </summary>
    public class CategoryController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryController(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        // GET: /category
        public async Task<IActionResult> Index()
        {
            var categoriesWithCount = await _categoryRepository.GetCategoriesWithCountAsync();
            return View(categoriesWithCount);
        }

        // GET: /category/details/{slug}
        public IActionResult Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction("Category", "News", new { slug });
        }
    }
}
