using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string Message { get; set; }
        public int? DocumentId { get; set; }
        public Document Document { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.Now;
    }
}