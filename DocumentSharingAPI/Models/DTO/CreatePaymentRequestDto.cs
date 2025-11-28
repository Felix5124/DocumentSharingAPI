using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models.DTO
{
    public class CreatePaymentRequestDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public required string SubscriptionType { get; set; } // "Monthly" hoặc "Yearly"
    }
}
