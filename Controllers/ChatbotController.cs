using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Models.Help;
using ProductManagement.Models.Repositories;
using ProductManagement.Services;

namespace ProductManagement.Controllers
{
    [IgnoreAntiforgeryToken]
    public class ChatbotController : Controller
    {
        private readonly IChatbotService _chatbotService;
        private readonly IProductRepository _productRepository;
        private readonly ICategorieRepository _categoryRepository;

        private const string SessionCookieName = "NexusBotSession";

        public ChatbotController(
            IChatbotService chatbotService,
            IProductRepository productRepository,
            ICategorieRepository categoryRepository)
        {
            _chatbotService = chatbotService;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // ── Helper: resolve or create session ID ──────────────────────────
        private string GetOrCreateSessionId()
        {
            if (!Request.Cookies.TryGetValue(SessionCookieName, out string? sessionId)
                || string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
                Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    SameSite = SameSiteMode.Lax
                });
            }
            return sessionId;
        }

        // ── Public Chatbot Pages ───────────────────────────────────────────

        // GET: /Chatbot
        public IActionResult Index()
        {
            ViewData["Categories"] = _categoryRepository.GetAll();
            return View();
        }

        // ── AJAX Endpoints ────────────────────────────────────────────────

        // POST: /Chatbot/SendMessage
        [HttpPost]
        public async Task<IActionResult> SendMessage(string? message, IFormFile? image)
        {
            string sessionId = GetOrCreateSessionId();
            string? userName = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

            try
            {
                var response = await _chatbotService.ProcessMessageAsync(
                    message, image, sessionId, userName);
                return Json(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = "Une erreur est survenue lors de l'analyse.",
                    details = ex.Message
                });
            }
        }

        // POST: /Chatbot/AddToCartAjax
        [HttpPost]
        public IActionResult AddToCartAjax(int productId)
        {
            var product = _productRepository.GetById(productId);
            if (product == null)
                return Json(new { success = false, message = "Produit introuvable." });

            if (product.QteStock <= 0)
                return Json(new { success = false, message = "Désolé, ce produit est en rupture de stock." });

            ListeCart.Instance.AddItem(product);
            int totalCount = ListeCart.Instance.Items.Sum(i => i.quantite);

            return Json(new
            {
                success = true,
                message = $"« {product.Name} » a été ajouté à votre panier !",
                cartCount = totalCount
            });
        }

        // GET: /Chatbot/StockSummary
        [HttpGet]
        public IActionResult StockSummary()
        {
            var products = _productRepository.GetAll();
            var totalProducts = products.Count;
            var inStock = products.Count(p => p.QteStock > 0);
            var lowStock = products.Count(p => p.QteStock > 0 && p.QteStock <= 5);
            var outOfStock = products.Count(p => p.QteStock <= 0);
            var totalUnits = products.Sum(p => p.QteStock);

            return Json(new { totalProducts, inStock, lowStock, outOfStock, totalUnits });
        }

        // ── Admin Analytics & History ─────────────────────────────────────

        // GET: /Chatbot/Analytics
        public async Task<IActionResult> Analytics()
        {
            var model = await _chatbotService.GetChatAnalyticsAsync();
            return View(model);
        }

        // GET: /Chatbot/History
        public async Task<IActionResult> History(int page = 1)
        {
            var model = await _chatbotService.GetChatHistoryAsync(page, pageSize: 15);
            return View(model);
        }
    }
}
