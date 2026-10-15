using Microsoft.AspNetCore.Http;
using ProductManagement.ViewModels;

namespace ProductManagement.Services
{
    public interface IChatbotService
    {
        Task<ChatbotResponseViewModel> ProcessMessageAsync(
            string? message,
            IFormFile? imageFile,
            string? sessionId = null,
            string? userName = null);

        Task<ChatbotHistoryViewModel> GetChatHistoryAsync(int page = 1, int pageSize = 20);
        Task<ChatbotAnalyticsViewModel> GetChatAnalyticsAsync();
    }
}
