using System.ComponentModel.DataAnnotations;

namespace DocumentSharingAPI.Models.DTO
{
    public class UpdateDocumentDto
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(255, MinimumLength = 3, ErrorMessage = "Tiêu đề phải từ 3 đến 255 ký tự")]
        public string Title { get; set; }

        public string? Description { get; set; } 

        [Required(ErrorMessage = "Danh mục không được để trống")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục hợp lệ")]
        public int CategoryId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Điểm yêu cầu phải là số không âm")]
        public int PointsRequired { get; set; }
        public int SchoolId { get; set; }
        public IFormFile? File { get; set; }
        public IFormFile? CoverImage { get; set; }
        public List<string>? Tags { get; set; }
    }
}
