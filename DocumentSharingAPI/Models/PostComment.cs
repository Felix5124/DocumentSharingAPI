using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class PostComment
    {
        [Key]
        public int PostCommentId { get; set; }
        public int PostId { get; set; }
        public Post Post { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}