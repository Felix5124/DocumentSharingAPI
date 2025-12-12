using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models.DTO
{
    /// <summary>
    /// DTO for updating document metadata only (no file changes)
    /// Used for editing title, description, category, and VIP flag
    /// </summary>
    public class UpdateDocumentMetadataDto
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(255, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 255 ký tự")]
        public string Title { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Danh mục không được để trống")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục hợp lệ")]
        public int CategoryId { get; set; }

        public bool IsVipOnly { get; set; }
    }
}
