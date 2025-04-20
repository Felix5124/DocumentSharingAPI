namespace DocumentSharingAPI.Models
{
    public class UserDocument
    {
        public int UserId { get; set; }
        public User User { get; set; }
        public int DocumentId { get; set; }
        public Document Document { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public string ActionType { get; set; } // Upload, Download, Library
    }
}