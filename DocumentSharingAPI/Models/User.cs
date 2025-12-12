using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public string? Bio { get; set; }  // Giới thiệu bản thân (nullable, backward compatible)
        public string? Settings { get; set; }  // JSON string chứa user settings (notifications, privacy, display)
        public bool IsAdmin { get; set; } = false;
        public bool IsLocked { get; set; } = false;
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CommentCount { get; set; }

        // VIP System
        public bool IsVip { get; set; } = false;
        public DateTime? VipExpiryDate { get; set; }
        public int VipDownloadsUsedToday { get; set; } = 0;
        public int RegularDownloadsUsedToday { get; set; } = 0;
        public int VipBonusDownloads { get; set; } = 0; // Bonus downloads VIP từ việc upload tài liệu
        public int RegularBonusDownloads { get; set; } = 0; // Bonus downloads thường từ việc upload tài liệu
        public DateTime LastDownloadResetDate { get; set; } = DateTime.Today;

        // Quan hệ
        public ICollection<Document> UploadedDocuments { get; set; }
        public ICollection<UserBadge> Badges { get; set; }
        public ICollection<Follow> Follows { get; set; }
        public ICollection<Follow> Followers { get; set; }
        public ICollection<VipSubscription> VipSubscriptions { get; set; }
    }
}