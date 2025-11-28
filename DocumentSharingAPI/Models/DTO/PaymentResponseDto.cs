namespace DocumentSharingAPI.Models.DTO
{
    public class PaymentResponseDto
    {
        public int PaymentId { get; set; }
        public required string OrderCode { get; set; }
        public int UserId { get; set; }
        public required string UserFullName { get; set; }
        public required string UserEmail { get; set; }
        public required string SubscriptionType { get; set; }
        public decimal Amount { get; set; }
        public required string Status { get; set; }
        public required string TransferContent { get; set; }
        public required string BankAccountNumber { get; set; }
        public required string BankName { get; set; }
        public required string AccountHolderName { get; set; }
        public string? QRCodeUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string? Note { get; set; }
    }
}
