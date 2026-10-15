using Microsoft.EntityFrameworkCore;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;

namespace ProductManagement.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly AppDbContext _context;

        public RecommendationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Content-based & Co-occurrence Recommendation:
        /// Calcule un score hybride combinant similarité de catégorie, proximité de prix et co-commandes passées.
        /// </summary>
        public async Task<List<Product>> GetSimilarProductsAsync(int productId, int count = 4)
        {
            var target = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (target == null)
            {
                return new List<Product>();
            }

            // Tous les autres produits actifs
            var candidates = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.ProductId != productId)
                .ToListAsync();

            if (!candidates.Any())
            {
                return new List<Product>();
            }

            // 1. Analyse collaborative : Identifier les co-achats dans l'historique des commandes
            var coOrderedProductNames = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.Items.Any(i => i.ProductName == target.Name))
                .SelectMany(o => o.Items)
                .Where(i => i.ProductName != target.Name)
                .GroupBy(i => i.ProductName)
                .Select(g => new { Name = g.Key, Frequency = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.Name, x => x.Frequency);

            // 2. Calcul du score pour chaque candidat
            var scoredProducts = candidates.Select(candidate =>
            {
                double score = 0.0;

                // Critère 1 : Même catégorie (+40 points)
                if (candidate.CategoryId == target.CategoryId)
                {
                    score += 40.0;
                }

                // Critère 2 : Proximité de prix (Distance relative, jusqu'à +30 points)
                if (target.Price > 0 && candidate.Price > 0)
                {
                    double priceRatio = Math.Abs(candidate.Price - target.Price) / (double)Math.Max(candidate.Price, target.Price);
                    double priceScore = Math.Max(0.0, 1.0 - priceRatio) * 30.0;
                    score += priceScore;
                }

                // Critère 3 : Fréquence de co-achat (+10 points par co-achat, max 30)
                if (coOrderedProductNames.TryGetValue(candidate.Name, out int freq))
                {
                    score += Math.Min(30.0, freq * 10.0);
                }

                // Bonus disponibilité en stock
                if (candidate.QteStock > 0)
                {
                    score += 5.0;
                }

                return new { Product = candidate, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Product)
            .ToList();

            return scoredProducts;
        }

        /// <summary>
        /// Recommandations intelligentes pour le panier ("Complétez votre panier")
        /// </summary>
        public async Task<List<Product>> GetCartRecommendationsAsync(IEnumerable<int> cartProductIds, int count = 4)
        {
            var cartIdList = cartProductIds?.ToList() ?? new List<int>();

            // Si le panier est vide, recommander les produits tendances / populaires
            if (!cartIdList.Any())
            {
                return await GetTrendingProductsAsync(count);
            }

            var cartProducts = await _context.Products
                .Where(p => cartIdList.Contains(p.ProductId))
                .ToListAsync();

            var cartProductNames = cartProducts.Select(p => p.Name).ToHashSet();
            var cartCategoryIds = cartProducts.Select(p => p.CategoryId).Distinct().ToList();

            // 1. Rechercher les articles co-achetés avec n'importe quel article du panier
            var coPurchasedNames = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.Items.Any(i => cartProductNames.Contains(i.ProductName)))
                .SelectMany(o => o.Items)
                .Where(i => !cartProductNames.Contains(i.ProductName))
                .GroupBy(i => i.ProductName)
                .OrderByDescending(g => g.Sum(x => x.Quantity))
                .Select(g => g.Key)
                .Take(count)
                .ToListAsync();

            var recommended = new List<Product>();

            if (coPurchasedNames.Any())
            {
                var coPurchasedProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => coPurchasedNames.Contains(p.Name) && !cartIdList.Contains(p.ProductId))
                    .ToListAsync();

                recommended.AddRange(coPurchasedProducts);
            }

            // 2. Si moins que 'count', compléter avec des produits de catégories similaires ou populaires non présents dans le panier
            if (recommended.Count < count)
            {
                int needed = count - recommended.Count;
                var alreadyPickedIds = recommended.Select(r => r.ProductId).Concat(cartIdList).ToHashSet();

                var complementary = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => !alreadyPickedIds.Contains(p.ProductId))
                    .OrderByDescending(p => cartCategoryIds.Contains(p.CategoryId))
                    .ThenByDescending(p => p.QteStock > 0)
                    .Take(needed)
                    .ToListAsync();

                recommended.AddRange(complementary);
            }

            return recommended.Take(count).ToList();
        }

        /// <summary>
        /// Produits tendances / Best-sellers
        /// </summary>
        public async Task<List<Product>> GetTrendingProductsAsync(int count = 4)
        {
            // Trouver les noms de produits les plus vendus
            var topSoldNames = await _context.OrderItems
                .GroupBy(i => i.ProductName)
                .OrderByDescending(g => g.Sum(x => x.Quantity))
                .Select(g => g.Key)
                .Take(count)
                .ToListAsync();

            var result = new List<Product>();

            if (topSoldNames.Any())
            {
                result = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => topSoldNames.Contains(p.Name))
                    .ToListAsync();
            }

            // Compléter si pas assez de ventes avec les produits ayant du stock
            if (result.Count < count)
            {
                var existingIds = result.Select(r => r.ProductId).ToHashSet();
                var extra = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => !existingIds.Contains(p.ProductId))
                    .OrderByDescending(p => p.ProductId)
                    .Take(count - result.Count)
                    .ToListAsync();

                result.AddRange(extra);
            }

            return result;
        }

        /// <summary>
        /// Résumé analytique pour le tableau de bord Admin / Statistiques
        /// </summary>
        public async Task<Dictionary<string, object>> GetAnalyticsSummaryAsync()
        {
            var totalOrders = await _context.Orders.CountAsync();
            var totalRevenue = await _context.Orders.SumAsync(o => (double)o.TotalAmount);
            var totalProducts = await _context.Products.CountAsync();
            var lowStockCount = await _context.Products.CountAsync(p => p.QteStock <= 3);

            // Top 5 produits les plus vendus
            var topSelling = await _context.OrderItems
                .GroupBy(i => i.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.Price * x.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            // Ventes par catégorie
            var allProductsWithCat = await _context.Products
                .Include(p => p.Category)
                .ToListAsync();

            var productCategoryMap = allProductsWithCat
                .ToDictionary(p => p.Name, p => p.Category?.CategoryName ?? "Non classé");

            var orderItems = await _context.OrderItems.ToListAsync();
            var salesByCategory = orderItems
                .GroupBy(i => productCategoryMap.TryGetValue(i.ProductName, out var cat) ? cat : "Autre")
                .Select(g => new
                {
                    CategoryName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Price * x.Quantity)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // Commandes récentes
            var recentOrders = await _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            // Produits en alerte de stock
            var lowStockProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.QteStock <= 3)
                .OrderBy(p => p.QteStock)
                .ToListAsync();

            return new Dictionary<string, object>
            {
                { "TotalOrders", totalOrders },
                { "TotalRevenue", totalRevenue },
                { "TotalProducts", totalProducts },
                { "LowStockCount", lowStockCount },
                { "TopSelling", topSelling },
                { "SalesByCategory", salesByCategory },
                { "RecentOrders", recentOrders },
                { "LowStockProducts", lowStockProducts }
            };
        }
    }
}
