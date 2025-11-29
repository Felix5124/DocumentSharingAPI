namespace DocumentSharingAPI.Models.DTO
{
    public class ChatMessageDto
    {
        public string Role { get; set; } // "user" hoặc "model"
        public string Text { get; set; }
    }
}