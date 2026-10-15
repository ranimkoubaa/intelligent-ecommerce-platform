/*
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;

namespace ProductManagement.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductRepository productRepository;
        private readonly ICategorieRepository categorieRepository;

        public ProductController(
            IProductRepository productRepository,
            ICategorieRepository categorieRepository)
        {
            this.productRepository = productRepository;
            this.categorieRepository = categorieRepository;
        }

        public IActionResult Index()
        {
            var products = productRepository.GetAll();
            return View(products);
        }

        public IActionResult Create()
        {
            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName"
            );

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product)
        {
            if (ModelState.IsValid)
            {
                productRepository.Add(product);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName"
            );

            return View(product);
        }

        public IActionResult Details(int id)
        {
            var product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        public IActionResult Edit(int id)
        {
            var product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName",
                product.CategoryId
            );

            return View(product);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product product)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                productRepository.Update(product);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName",
                product.CategoryId
            );

            return View(product);
        }
        public IActionResult Delete(int id)
        {
            var product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            productRepository.Delete(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;
using ProductManagement.ViewModels;

namespace ProductManagement.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ProductController : Controller
    {
        private readonly IProductRepository productRepository;
        private readonly ICategorieRepository categorieRepository;
        private readonly IWebHostEnvironment hostingEnvironment;
        private readonly ProductManagement.Services.IRecommendationService recommendationService;

        public ProductController(
            IProductRepository productRepository,
            ICategorieRepository categorieRepository,
            IWebHostEnvironment hostingEnvironment,
            ProductManagement.Services.IRecommendationService recommendationService)
        {
            this.productRepository = productRepository;
            this.categorieRepository = categorieRepository;
            this.hostingEnvironment = hostingEnvironment;
            this.recommendationService = recommendationService;
        }

        // GET: Product
        [AllowAnonymous]
        public IActionResult Index(int? categoryId, string? sortBy, int page = 1)
        {
            int pageSize = 6; // Nombre de produits par page
            var categories = categorieRepository.GetAll();
            // Passer les catégories à la vue
            ViewData["Categories"] = categories;

            // Récupérer les produits en fonction de categoryId, s'il est spécifié
            IQueryable<Product> productsQuery = productRepository.GetAllProducts();

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
            }

            // Tri ordonné
            productsQuery = sortBy switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                "name" => productsQuery.OrderBy(p => p.Name),
                "newest" => productsQuery.OrderByDescending(p => p.ProductId),
                _ => productsQuery.OrderBy(p => p.ProductId)
            };

            // Pagination
            var totalProducts = productsQuery.Count();
            var products = productsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.CategoryId = categoryId; // Passer categoryId à la vue
            ViewBag.SortBy = sortBy;

            return View(products);
        }

        [AllowAnonymous]
        public IActionResult Search(string name)
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;

            var products = productRepository.FindByName(name);
            return View("Index", products);
        }

        // GET: Product/Create
        public IActionResult Create()
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;

            ViewBag.CategoryId = new SelectList(
                categories,
                "CategoryId",
                "CategoryName"
            );

            return View();
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                string uniqueFileName = null;

                if (model.ImagePath != null)
                {
                    string uploadsFolder = Path.Combine(
                        hostingEnvironment.WebRootPath,
                        "images"
                    );

                    uniqueFileName =
                        Guid.NewGuid().ToString() + "_" + model.ImagePath.FileName;

                    string filePath = Path.Combine(
                        uploadsFolder,
                        uniqueFileName
                    );

                    model.ImagePath.CopyTo(
                        new FileStream(filePath, FileMode.Create)
                    );
                }

                Product newProduct = new Product
                {
                    Name = model.Name,
                    Price = model.Price,
                    QteStock = model.QteStock,
                    CategoryId = model.CategoryId,
                    Image = uniqueFileName
                };

                productRepository.Add(newProduct);

                return RedirectToAction(
                    "Details",
                    new { id = newProduct.ProductId }
                );
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName"
            );

            return View(model);
        }

        // GET: Product/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;

            var product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.RecommendedProducts = await recommendationService.GetSimilarProductsAsync(id, 4);

            return View(product);
        }

        // GET: Product/Edit/5
        public IActionResult Edit(int id)
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;

            ViewBag.CategoryId = new SelectList(
                categories,
                "CategoryId",
                "CategoryName"
            );

            Product product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            EditViewModel productEditViewModel = new EditViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Price = product.Price,
                QteStock = product.QteStock,
                CategoryId = product.CategoryId,
                ExistingImagePath = product.Image
            };

            return View(productEditViewModel);
        }

        // POST: Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditViewModel model)
        {
            ViewBag.CategoryId = new SelectList(
                categorieRepository.GetAll(),
                "CategoryId",
                "CategoryName"
            );

            if (ModelState.IsValid)
            {
                Product product = productRepository.GetById(model.ProductId);

                if (product == null)
                {
                    return NotFound();
                }

                product.Name = model.Name;
                product.Price = model.Price;
                product.QteStock = model.QteStock;
                product.CategoryId = model.CategoryId;

                if (model.ImagePath != null)
                {
                    if (model.ExistingImagePath != null)
                    {
                        string filePath = Path.Combine(
                            hostingEnvironment.WebRootPath,
                            "images",
                            model.ExistingImagePath
                        );

                        System.IO.File.Delete(filePath);
                    }

                    product.Image = ProcessUploadedFile(model);
                }

                Product updatedProduct = productRepository.Update(product);

                if (updatedProduct != null)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    return NotFound();
                }
            }

            ViewData["Categories"] = categorieRepository.GetAll();
            return View(model);
        }

        [NonAction]
        private string ProcessUploadedFile(EditViewModel model)
        {
            string uniqueFileName = null;

            if (model.ImagePath != null)
            {
                string uploadsFolder = Path.Combine(
                    hostingEnvironment.WebRootPath,
                    "images"
                );

                uniqueFileName =
                    Guid.NewGuid().ToString() + "_" + model.ImagePath.FileName;

                string filePath = Path.Combine(
                    uploadsFolder,
                    uniqueFileName
                );

                using (var fileStream = new FileStream(
                    filePath,
                    FileMode.Create))
                {
                    model.ImagePath.CopyTo(fileStream);
                }
            }

            return uniqueFileName;
        }

        // GET: Product/Delete/5
        public IActionResult Delete(int id)
        {
            var categories = categorieRepository.GetAll();
            ViewData["Categories"] = categories;

            var product = productRepository.GetById(id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Product/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            productRepository.Delete(id);

            return RedirectToAction(nameof(Index));
        }
    }
}