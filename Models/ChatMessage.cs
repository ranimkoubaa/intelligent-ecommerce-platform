using System;
using System.ComponentModel.DataAnnotations;

namespace ProductManagement.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public string? SessionId { get; set; }

        public string? UserName { get; set; }

        [Required]
        public string UserMessage { get; set; } = string.Empty;

        [Required]
        public string BotResponse { get; set; } = string.Empty;

        public string? AttachedImageUrl { get; set; }

        public string? DetectedIntent { get; set; }

        public int? MatchedProductId { get; set; }

        public Product? MatchedProduct { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
