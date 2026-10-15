namespace ProductManagement.ViewModels
{
    public class CartItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public float Price { get; set; }
    }
}
