using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class VipSubscription
    {
        [Key]
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SubscriptionType { get; set; } // "Monthly" hoặc "Yearly"
        public decimal Price { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}