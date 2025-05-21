namespace DocumentSharingAPI.Models
{
    public class UserRankingItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; } // Giữ lại email để có thể dùng cho việc liên hệ hoặc hiển thị thêm
        public string AvatarUrl { get; set; }
        public int Value { get; set; } // Giá trị để xếp hạng (điểm, số lượng, v.v.)
        public string ValueDescription { get; set; } // Mô tả cho giá trị (ví dụ: "điểm", "tài liệu tải lên") 
    }
}
