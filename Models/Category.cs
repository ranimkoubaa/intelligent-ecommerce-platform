using System.ComponentModel.DataAnnotations;

namespace ProductManagement.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required]
        [Display(Name = "Nom")]
        public string CategoryName { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}