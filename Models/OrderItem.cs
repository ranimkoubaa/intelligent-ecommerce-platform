namespace ProductManagement.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public float Price { get; set; }
    }
}
