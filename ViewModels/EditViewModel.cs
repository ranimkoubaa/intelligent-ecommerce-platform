using ProductManagement.Models;

namespace ProductManagement.ViewModels
{
    public class EditViewModel : CreateViewModel
    {
        public string? ExistingImagePath { get; set; }
    }
}