using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class School
    {
        [Key]
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public string ExternalUrl { get; set; }
    }
}