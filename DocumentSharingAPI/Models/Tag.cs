using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Tag
    {
        [Key]
        public int TagId { get; set; }

        [Required(ErrorMessage = "Tên tag không được để trống")]
        [MaxLength(100, ErrorMessage = "Tên tag không được vượt quá 100 ký tự")]
        public string Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new HashSet<DocumentTag>();
    }
}
