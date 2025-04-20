using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        public string Name { get; set; }
        public string Type { get; set; }

        public ICollection<Document> Documents { get; set; }
    }
}