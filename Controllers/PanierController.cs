using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Models;
using ProductManagement.Models.Help;
using ProductManagement.Models.Repositories;
using ProductManagement.ViewModels;

namespace ProductManagement.Controllers
{
    public class PanierController : Controller
    {
        readonly IProductRepository productRepository;
        readonly IOrderRepository orderRepository;
        readonly ICategorieRepository categorieRepository;
        readonly UserManager<IdentityUser> userManager;
        readonly ProductManagement.Services.IRecommendationService recommendationService;

        public PanierController(
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            ICategorieRepository categorieRepository,
            UserManager<IdentityUser> userManager,
            ProductManagement.Services.IRecommendationService recommendationService)
        {
            this.productRepository = productRepository;
            this.orderRepository = orderRepository;
            this.categorieRepository = categorieRepository;
            this.userManager = userManager;
            this.recommendationService = recommendationService;
        }

        public async Task<ActionResult> Index()
        {
            ViewBag.Liste = ListeCart.Instance.Items;
            ViewBag.total = ListeCart.Instance.GetSubTotal();
            ViewData["Categories"] = categorieRepository.GetAll();

            var cartProductIds = ListeCart.Instance.Items
                .Where(i => i.Prod != null)
                .Select(i => i.Prod!.ProductId);

            ViewBag.RecommendedProducts = await recommendationService.GetCartRecommendationsAsync(cartProductIds, 4);

            return View();
        }

        public async Task<ActionResult> AddProduct(int id)
        {
            Product pp = productRepository.GetById(id);
            if (pp != null)
            {
                ListeCart.Instance.AddItem(pp);
            }
            ViewBag.Liste = ListeCart.Instance.Items;
            ViewBag.total = ListeCart.Instance.GetSubTotal();
            ViewData["Categories"] = categorieRepository.GetAll();

            var cartProductIds = ListeCart.Instance.Items
                .Where(i => i.Prod != null)
                .Select(i => i.Prod!.ProductId);

            ViewBag.RecommendedProducts = await recommendationService.GetCartRecommendationsAsync(cartProductIds, 4);

            return View();
        }

        [HttpPost]
        public ActionResult PlusProduct(int id)
        {
            Product pp = productRepository.GetById(id);
            if (pp != null)
            {
                ListeCart.Instance.AddItem(pp);
            }
            Item? trouve = null;
            foreach (Item a in ListeCart.Instance.Items)
            {
                if (pp != null && a.Prod != null && a.Prod.ProductId == pp.ProductId)
                    trouve = a;
            }
            var results = new
            {
                ct = 1,
                Total = ListeCart.Instance.GetSubTotal(),
                Quatite = trouve != null ? trouve.quantite : 0,
                TotalRow = trouve != null ? trouve.TotalPrice : 0
            };
            return Json(results);
        }

        [HttpPost]
        public ActionResult MinusProduct(int id)
        {
            Product pp = productRepository.GetById(id);
            if (pp != null)
            {
                ListeCart.Instance.SetLessOneItem(pp);
            }
            Item? trouve = null;
            foreach (Item a in ListeCart.Instance.Items)
            {
                if (pp != null && a.Prod != null && a.Prod.ProductId == pp.ProductId)
                    trouve = a;
            }
            if (trouve != null)
            {
                var results = new
                {
                    Total = ListeCart.Instance.GetSubTotal(),
                    Quatite = trouve.quantite,
                    TotalRow = trouve.TotalPrice,
                    ct = 1
                };
                return Json(results);
            }
            else
            {
                var results = new
                {
                    ct = 0
                };
                return Json(results);
            }
        }

        [HttpPost]
        public ActionResult RemoveProduct(int id)
        {
            Product pp = productRepository.GetById(id);
            if (pp != null)
            {
                ListeCart.Instance.RemoveItem(pp);
            }
            var results = new
            {
                Total = ListeCart.Instance.GetSubTotal(),
            };
            return Json(results);
        }

        [Authorize]
        // GET: /Panier/Checkout
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
            }
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
            }
            var cartItems = ListeCart.Instance.Items.ToList();
            var totalAmount = ListeCart.Instance.GetSubTotal();
            var viewModel = new OrderViewModel
            {
                CartItems = cartItems.Select(item => new CartItemViewModel
                {
                    ProductName = item.Prod != null ? item.Prod.Name : string.Empty,
                    Quantity = item.quantite,
                    Price = item.Prod != null ? item.Prod.Price : 0
                }).ToList(),
                TotalAmount = totalAmount
            };
            ViewData["Categories"] = categorieRepository.GetAll();
            return View(viewModel);
        }

        // POST : /Panier/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Checkout(OrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = userManager.GetUserAsync(User).Result;
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Utilisateur non authentifié.";
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }
                // Recharger les articles du panier depuis la source (ListeCart)
                var cartItems = ListeCart.Instance.Items.ToList();
                model.CartItems = cartItems.Select(item => new CartItemViewModel
                {
                    ProductName = item.Prod != null ? item.Prod.Name : string.Empty,
                    Quantity = item.quantite,
                    Price = item.Prod != null ? item.Prod.Price : 0
                }).ToList();
                model.TotalAmount = ListeCart.Instance.GetSubTotal();

                var order = new Order
                {
                    CustomerName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    TotalAmount = model.TotalAmount,
                    OrderDate = DateTime.Now,
                    UserId = user.Id,
                    Items = model.CartItems.Select(item => new OrderItem
                    {
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = item.Price
                    }).ToList()
                };

                orderRepository.Add(order);
                ListeCart.Instance.Items.Clear();
                TempData["SuccessMessage"] = "Votre commande a été passée avec succès.";
                return RedirectToAction("Confirmation", new { orderId = order.Id });
            }

            TempData["ErrorMessage"] = "Une erreur est survenue. Veuillez vérifier les informations.";
            ViewData["Categories"] = categorieRepository.GetAll();
            return View(model);
        }

        // GET: /Panier/Confirmation
        public IActionResult Confirmation(int orderId)
        {
            var order = orderRepository.GetById(orderId);
            ViewData["Categories"] = categorieRepository.GetAll();
            return View(order);
        }
    }
}
