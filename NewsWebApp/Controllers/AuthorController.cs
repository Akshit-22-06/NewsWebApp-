using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewsWebApp.Repositories.Interfaces;

namespace NewsWebApp.Controllers
{
    /// <summary>
    /// Controller for viewing journalist and author profiles and their published articles.
    /// </summary>
    public class AuthorController : Controller
    {
        private readonly IAuthorRepository _authorRepository;

        public AuthorController(IAuthorRepository authorRepository)
        {
            _authorRepository = authorRepository;
        }

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
    }
}
