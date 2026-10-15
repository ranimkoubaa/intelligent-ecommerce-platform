using ProductManagement.Models;

namespace ProductManagement.Models.Help
{
    public class Item
    {
        public int quantite { get; set; }
        public Product? _product = null;
        public Product? Prod
        {
            get { return _product; }
            set { _product = value; }
        }
        public string Description
        {
            get { return _product?.Name ?? string.Empty; }
        }
        public float UnitPrice
        {
            get { return _product?.Price ?? 0; }
        }
        public int categoryId
        {
            get { return _product?.CategoryId ?? 0; }
        }
        public Category? category
        {
            get { return _product?.Category; }
        }
        public float TotalPrice
        {
            get { return (_product?.Price ?? 0) * quantite; }
        }
        public Item(Product p)
        {
            this.Prod = p;
        }
        public bool Equals(Item? item)
        {
            if (item == null || item.Prod == null || this.Prod == null) return false;
            return item.Prod.ProductId == this.Prod.ProductId;
        }
    }
}
