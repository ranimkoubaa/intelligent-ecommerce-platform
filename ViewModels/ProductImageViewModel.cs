using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ProductManagement.ViewModels
{
    public class ProductImageViewModel
    {
        public int ProductId { get; set; }

        public string? Name { get; set; }

        public float Price { get; set; }

        public int QteStock { get; set; }

        public int CategoryId { get; set; }

        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; }
    }
}