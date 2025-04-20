using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Comment
    {
        [Key]
        public int CommentId { get; set; }
        public int DocumentId { get; set; }
        public Document Document { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}