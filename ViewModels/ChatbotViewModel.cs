using System;
using System.Collections.Generic;

namespace ProductManagement.ViewModels
{
    public class ChatbotProductDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public float Price { get; set; }
        public int QteStock { get; set; }
        public string StockStatus { get; set; } = string.Empty;
        public string StockBadgeClass { get; set; } = "success";
        public string? ImageUrl { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? ConfidencePercent { get; set; }
    }

    public class ChatbotResponseViewModel
    {
        public string Reply { get; set; } = string.Empty;
        public bool HasImageMatch { get; set; }
        public ChatbotProductDto? IdentifiedProduct { get; set; }
        public List<ChatbotProductDto> Products { get; set; } = new();
        public List<string> QuickReplies { get; set; } = new();
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm");
        public string DetectedIntent { get; set; } = string.Empty;
        public int? MatchedProductId { get; set; }
    }

    // ── Hybrid History & Analytics ViewModels ──────────────────────────

    public class ChatMessageViewModel
    {
        public int Id { get; set; }
        public string? SessionId { get; set; }
        public string? UserName { get; set; }
        public string UserMessage { get; set; } = string.Empty;
        public string BotResponse { get; set; } = string.Empty;
        public string? AttachedImageUrl { get; set; }
        public string? DetectedIntent { get; set; }
        public string? MatchedProductName { get; set; }
        public string? MatchedProductImage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChatbotHistoryViewModel
    {
        public List<ChatMessageViewModel> Messages { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class IntentStatDto
    {
        public string Intent { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Icon { get; set; } = "bi-chat";
    }

    public class TopProductStatDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Mentions { get; set; }
    }

    public class ChatbotAnalyticsViewModel
    {
        public int TotalConversations { get; set; }
        public int TotalSessions { get; set; }
        public int ImageSearches { get; set; }
        public int TodayConversations { get; set; }
        public List<IntentStatDto> TopIntents { get; set; } = new();
        public List<TopProductStatDto> TopProducts { get; set; } = new();
        public List<ChatMessageViewModel> RecentMessages { get; set; } = new();
    }
}
