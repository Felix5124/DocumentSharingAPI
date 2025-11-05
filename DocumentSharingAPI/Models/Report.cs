using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Report
    {
        [Key]
        public int ReportId { get; set; }

        [Required]
        public int DocumentId { get; set; }
        public Document Document { get; set; }

        [Required]
        public int ReporterUserId { get; set; } // Người báo cáo
        public User User { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; }

        public string Details { get; set; } // Chi tiết báo cáo

        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Trạng thái báo cáo: Pending, Resolved, Rejected
    }
}