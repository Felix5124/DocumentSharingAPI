namespace DocumentSharingAPI.Services
{
    public interface IGeminiChatService
    {
        Task<string> GetChatbotResponseAsync(string userMessage, int userId);
    }
}
