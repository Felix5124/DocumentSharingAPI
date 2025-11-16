using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class BankAccount
    {
        [Key]
        public int BankAccountId { get; set; }
        
        [Required]
        public required string BankName { get; set; } // VD: "Vietcombank", "Techcombank"
        
        [Required]
        public required string BankCode { get; set; } // Mã ngân hàng: VCB, TCB, MB...
        
        [Required]
        public required string AccountNumber { get; set; } // Số tài khoản
        
        [Required]
        public required string AccountHolderName { get; set; } // Tên chủ tài khoản
        
        public bool IsActive { get; set; } = true; // TK đang sử dụng hay không
        
        public bool IsDefault { get; set; } = false; // TK mặc định
        
        public string? QRTemplate { get; set; } // Template URL để generate QR
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
