namespace DocumentSharingAPI.Models
{
    public class UserBadge
    {
        public int UserId { get; set; }
        public User User { get; set; }
        public int BadgeId { get; set; }
        public Badge Badge { get; set; }
        public DateTime EarnedAt { get; set; } = DateTime.Now;
    }
}