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
        public int? SchoolId { get; set; } 
        public School School { get; set; }
        public int Points { get; set; } = 0;
        public string Level { get; set; } = "Newbie";
        public bool IsAdmin { get; set; } = false;
        public bool IsLocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CommentCount { get; set; }

<<<<<<< Updated upstream
=======
        // VIP System
        public bool IsVip { get; set; } = false;
        public DateTime? VipExpiryDate { get; set; }
        public int VipDownloadsUsedToday { get; set; } = 0;
        public int RegularDownloadsUsedToday { get; set; } = 0;
        public int VipBonusDownloads { get; set; } = 0; // Bonus downloads VIP từ việc upload tài liệu
        public int RegularBonusDownloads { get; set; } = 0; // Bonus downloads thường từ việc upload tài liệu
        public DateTime LastDownloadResetDate { get; set; } = DateTime.Today;

>>>>>>> Stashed changes
        // Quan hệ
        public ICollection<Document> UploadedDocuments { get; set; }
        public ICollection<UserBadge> Badges { get; set; }
        public ICollection<Follow> Follows { get; set; }
        public ICollection<Follow> Followers { get; set; }
    }
}