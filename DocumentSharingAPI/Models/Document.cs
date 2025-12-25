using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
        public string FileUrl { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public int UploadedBy { get; set; }
        public User User { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public int DownloadCount { get; set; } = 0;
        public bool IsVipOnly { get; set; } = false; // Tài liệu VIP hay thường
        
        // THAY THẾ: bool IsApproved bằng ApprovalStatus
        [Required]
        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "Pending"; // Các giá trị: "Pending", "SemiApproved", "Approved", "Rejected", "Suspended"
        
        // THÊM: trường đếm số lượt báo cáo
        public int ReportCount { get; set; } = 0;
        
        // Lưu trạng thái trước khi bị tạm khóa (để admin có thể phục hồi)
        [MaxLength(20)]
        public string? PreviousApprovalStatus { get; set; }
        
        public bool IsLock { get; set; } = false;
        public string? CoverImageUrl { get; set; }
        public int ApprovalPriority { get; set; } = 0; // Độ ưu tiên duyệt (VIP = 1, thường = 0)
        
        public ICollection<Comment> Comments { get; set; }
        public ICollection<UserDocument> UserDocuments { get; set; }

        public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new HashSet<DocumentTag>();

    }
}