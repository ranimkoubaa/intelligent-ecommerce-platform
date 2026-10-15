using ProductManagement.Models;

namespace ProductManagement.ViewModels
{
    public class ProduitPaginationViewModel
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public int PageActuelle { get; set; }
        public int TotalPages { get; set; }
    }
}
