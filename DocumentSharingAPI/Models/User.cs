using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace DocumentSharingAPI.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string FirebaseUid { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? School { get; set; }
        public int Points { get; set; } = 0;
        public string Level { get; set; } = "Newbie";
        public bool IsAdmin { get; set; } = false;
        public bool IsLocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CommentCount { get; set; }

        // Quan hệ
        public ICollection<Document> UploadedDocuments { get; set; }
        public ICollection<UserBadge> Badges { get; set; }
        public ICollection<Follow> Follows { get; set; }
        public ICollection<Follow> Followers { get; set; }
    }
}