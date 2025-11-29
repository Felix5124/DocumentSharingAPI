using System.Collections.Generic;
using DocumentSharingAPI.Models.DTO;

namespace DocumentSharingAPI.Services
{
    public interface IGeminiChatService
    {
        // Thêm tham số history
        Task<string> GetChatbotResponseAsync(string userMessage, int userId, List<ChatMessageDto>? history);
    }
}
