using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }
        
        [Required]
        public required string OrderCode { get; set; } // Mã đơn hàng unique: VIP20250115001
        
        public int UserId { get; set; }
        public User? User { get; set; }
        
        [Required]
        public required string SubscriptionType { get; set; } // "Monthly" hoặc "Yearly"
        
        [Required]
        public decimal Amount { get; set; } // Số tiền
        
        public string Status { get; set; } = "Pending"; // Pending, Completed, Cancelled, Expired
        
        public string? TransferContent { get; set; } // Nội dung chuyển khoản mà user cần ghi
        
        public string? BankAccountNumber { get; set; } // Số TK ngân hàng nhận
        public string? BankName { get; set; } // Tên ngân hàng
        public string? AccountHolderName { get; set; } // Tên chủ TK
        
        public string? QRCodeUrl { get; set; } // URL của mã QR (nếu generate)
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; } // Thời điểm admin xác nhận
        public DateTime? ExpiredAt { get; set; } // Đơn hàng hết hạn sau 24h
        
        public int? ConfirmedByAdminId { get; set; } // Admin nào xác nhận
        
        public string? Note { get; set; } // Ghi chú thêm
    }
}
