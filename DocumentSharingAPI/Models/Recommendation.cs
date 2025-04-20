using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Recommendation
    {
        [Key]
        public int RecommendationId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public int DocumentId { get; set; }
        public Document Document { get; set; }
        public DateTime InteractedAt { get; set; } = DateTime.Now;
    }
}