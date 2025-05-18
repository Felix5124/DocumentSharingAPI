using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Follow
    {
        [Key]
        public int FollowId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public int FollowedUserId { get; set; }
        public User FollowedUser { get; set; }
        public int? CategoryId { get; set; }
        public Category Category { get; set; }
        public DateTime FollowedAt { get; set; } = DateTime.Now;
    }
}