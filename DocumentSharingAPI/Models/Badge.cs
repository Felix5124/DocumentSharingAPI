using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Badge
    {
        [Key]
        public int BadgeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ICollection<UserBadge> UserBadges { get; set; }
    }
}