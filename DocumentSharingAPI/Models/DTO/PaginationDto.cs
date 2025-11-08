using System.Collections.Generic;

namespace DocumentSharingAPI.Models.DTO
{
    public class ReportQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10; // Mặc định 10 báo cáo mỗi trang
        public string? Reason { get; set; } // Dùng để lọc theo lý do
        public string SortBy { get; set; } = "newest"; // Mặc định sắp xếp theo mới nhất
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    // DTO cho kết quả báo cáo đã được nhóm
    public class GroupedReportDto
    {
        public int DocumentId { get; set; }
        public string DocumentTitle { get; set; }
        public int ReportCount { get; set; }
        public System.DateTime LatestReportDate { get; set; }
        public List<string> Reasons { get; set; } // Danh sách các lý do báo cáo
    }
}