using ProductManagement.Models;

namespace ProductManagement.Services
{
    public interface IRecommendationService
    {
        Task<List<Product>> GetSimilarProductsAsync(int productId, int count = 4);
        Task<List<Product>> GetCartRecommendationsAsync(IEnumerable<int> cartProductIds, int count = 4);
        Task<List<Product>> GetTrendingProductsAsync(int count = 4);
        Task<Dictionary<string, object>> GetAnalyticsSummaryAsync();
    }
}
