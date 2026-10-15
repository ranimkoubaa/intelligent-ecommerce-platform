using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;

namespace ProductManagement.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class CategoryController : Controller
    {
        private readonly ICategorieRepository categorieRepository;

        public CategoryController(ICategorieRepository categorieRepository)
        {
            this.categorieRepository = categorieRepository;
        }

        // GET: CategoryController
        [AllowAnonymous]
        public IActionResult Index()
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;
            return View(categories);
        }

        public IActionResult Create()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                categorieRepository.Add(category);
                return RedirectToAction(nameof(Index));
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            return View(category);
        }
        public IActionResult Edit(int id)
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            var category = categorieRepository.GetById(id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Category category)
        {
            if (id != category.CategoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                categorieRepository.Update(category);
                return RedirectToAction(nameof(Index));
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            return View(category);
        }
        public IActionResult Details(int id)
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            var category = categorieRepository.GetById(id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }
        public IActionResult Delete(int id)
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            var category = categorieRepository.GetById(id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            categorieRepository.Delete(id);
            return RedirectToAction(nameof(Index));
        }
    }
}