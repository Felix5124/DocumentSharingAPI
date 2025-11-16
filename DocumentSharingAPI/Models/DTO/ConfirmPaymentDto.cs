using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models.DTO
{
    public class ConfirmPaymentDto
    {
        [Required]
        public int PaymentId { get; set; }
        
        [Required]
        public int AdminId { get; set; }
        
        public string? Note { get; set; }
    }
}
