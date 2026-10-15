using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;

namespace ProductManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ICategorieRepository categorieRepository;
        private readonly ProductManagement.Services.IRecommendationService recommendationService;

        public HomeController(
            ILogger<HomeController> logger,
            ICategorieRepository categorieRepository,
            ProductManagement.Services.IRecommendationService recommendationService)
        {
            _logger = logger;
            this.categorieRepository = categorieRepository;
            this.recommendationService = recommendationService;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            ViewBag.TrendingProducts = await recommendationService.GetTrendingProductsAsync(4);
            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Categories"] = categorieRepository.GetAll();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
