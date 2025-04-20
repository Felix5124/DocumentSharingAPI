using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Post
    {
        [Key]
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int ViewCount { get; set; } = 0;
        public ICollection<PostComment> Comments { get; set; }
    }
}