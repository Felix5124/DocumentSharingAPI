using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
        public string FileUrl { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public int UploadedBy { get; set; }
        public User User { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public int DownloadCount { get; set; } = 0;
        public int PointsRequired { get; set; } = 0;
        public bool IsApproved { get; set; } = false;
        public bool IsLock { get; set; } = false;
        public ICollection<Comment> Comments { get; set; }
        public ICollection<UserDocument> UserDocuments { get; set; }
    }
}