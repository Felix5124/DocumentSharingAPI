using DocumentSharingAPI.Models;
using DocumentSharingAPI.Models.DTO;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDocumentRepository _documentRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IDocumentStatusService _documentStatusService; // Thêm dòng này

        public ReportsController(AppDbContext context, IDocumentRepository documentRepository, INotificationRepository notificationRepository, IDocumentStatusService documentStatusService) // Thêm tham số
        {
            _context = context;
            _documentRepository = documentRepository;
            _notificationRepository = notificationRepository;
            _documentStatusService = documentStatusService; // Thêm dòng này
        }

        public class ReportModel
        {
            [Required]
            public int DocumentId { get; set; }
            [Required]
            public int ReporterUserId { get; set; }
            [Required]
            public string Reason { get; set; }
            public string Details { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] ReportModel model)
        {
            var document = await _documentRepository.GetByIdAsync(model.DocumentId);
            if (document == null)
                return NotFound("Tài liệu không tồn tại.");

            if (document.ApprovalStatus == "Pending")
                return BadRequest(new { message = "Không thể báo cáo tài liệu đang chờ duyệt." });

            // --- BẮT ĐẦU THAY ĐỔI ---
            // Kiểm tra xem người dùng có báo cáo nào đang hoạt động (chưa bị từ chối) cho tài liệu này không.
            var existingReport = await _context.Reports
                .FirstOrDefaultAsync(r =>
                    r.ReporterUserId == model.ReporterUserId &&
                    r.DocumentId == model.DocumentId &&
                    r.Status != "Rejected"); // <-- THÊM ĐIỀU KIỆN QUAN TRỌNG NÀY

            if (existingReport != null)
            {
                // Nếu đã tồn tại báo cáo đang chờ hoặc đã được xử lý, trả về lỗi.
                return Conflict(new { message = "Bạn đã có một báo cáo đang chờ xử lý cho tài liệu này." });
            }
            // --- KẾT THÚC THAY ĐỔI ---

            var report = new Report
            {
                DocumentId = model.DocumentId,
                ReporterUserId = model.ReporterUserId,
                Reason = model.Reason,
                Details = model.Details,
                Status = "Pending",
                ReportedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);

            // Tăng số lượt report trên tài liệu
            document.ReportCount++;
            await _documentRepository.UpdateAsync(document);

            // THAY THẾ TOÀN BỘ KHỐI `if` BẰNG DÒNG SAU:
            await _documentStatusService.CheckAndPotentiallyDemoteDocumentAsync(document.DocumentId);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Báo cáo của bạn đã được ghi nhận. Cảm ơn bạn đã đóng góp!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllReports([FromQuery] ReportQueryParameters queryParams)
        {
            // Bắt đầu với IQueryable để xây dựng truy vấn
            var query = _context.Reports.Where(r => r.Status == "Pending");

            // 1. Lọc (Filtering)
            if (!string.IsNullOrEmpty(queryParams.Reason))
            {
                query = query.Where(r => r.Reason == queryParams.Reason);
            }

            // 2. Nhóm các báo cáo theo DocumentId
            var groupedQuery = query.GroupBy(r => new { r.DocumentId, r.Document.Title, r.Document.IsLock, r.Document.ApprovalStatus }) // Group thêm IsLock, Status
                                    .Select(g => new GroupedReportDto
                                    {
                                        DocumentId = g.Key.DocumentId,
                                        DocumentTitle = g.Key.Title,
                                        ReportCount = g.Count(),
                                        LatestReportDate = g.Max(r => r.ReportedAt),
                                        Reasons = g.Select(r => r.Reason).Distinct().ToList(),
                                        // Map dữ liệu
                                        IsLocked = g.Key.IsLock,
                                        ApprovalStatus = g.Key.ApprovalStatus
                                    });

            // 3. Sắp xếp (Sorting)
            switch (queryParams.SortBy?.ToLower())
            {
                case "most_reported":
                    groupedQuery = groupedQuery.OrderByDescending(g => g.ReportCount);
                    break;
                case "oldest":
                    groupedQuery = groupedQuery.OrderBy(g => g.LatestReportDate);
                    break;
                default: // "newest"
                    groupedQuery = groupedQuery.OrderByDescending(g => g.LatestReportDate);
                    break;
            }

            // 4. Phân trang (Pagination)
            var totalCount = await groupedQuery.CountAsync();
            var items = await groupedQuery
                .Skip((queryParams.PageNumber - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            var result = new PagedResult<GroupedReportDto>
            {
                Items = items,
                PageNumber = queryParams.PageNumber,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)queryParams.PageSize)
            };

            return Ok(result);
        }

        [HttpGet("processed")]
        public async Task<IActionResult> GetProcessedReports([FromQuery] ReportQueryParameters queryParams)
        {
            // Bắt đầu với IQueryable để xây dựng truy vấn
            var query = _context.Reports.Where(r => r.Status == "Resolved" || r.Status == "Rejected");

            // 1. Lọc (Filtering)
            if (!string.IsNullOrEmpty(queryParams.Reason))
            {
                query = query.Where(r => r.Reason == queryParams.Reason);
            }

            // 2. Nhóm các báo cáo theo DocumentId
            var groupedQuery = query.GroupBy(r => new { r.DocumentId, r.Document.Title, r.Document.IsLock, r.Document.ApprovalStatus }) // Group thêm IsLock, Status
                                    .Select(g => new GroupedReportDto
                                    {
                                        DocumentId = g.Key.DocumentId,
                                        DocumentTitle = g.Key.Title,
                                        ReportCount = g.Count(),
                                        LatestReportDate = g.Max(r => r.ReportedAt),
                                        Reasons = g.Select(r => r.Reason).Distinct().ToList(),
                                        // Map dữ liệu
                                        IsLocked = g.Key.IsLock,
                                        ApprovalStatus = g.Key.ApprovalStatus
                                    });

            // 3. Sắp xếp (Sorting)
            switch (queryParams.SortBy?.ToLower())
            {
                case "most_reported":
                    groupedQuery = groupedQuery.OrderByDescending(g => g.ReportCount);
                    break;
                case "oldest":
                    groupedQuery = groupedQuery.OrderBy(g => g.LatestReportDate);
                    break;
                default: // "newest"
                    groupedQuery = groupedQuery.OrderByDescending(g => g.LatestReportDate);
                    break;
            }

            // 4. Phân trang (Pagination)
            var totalCount = await groupedQuery.CountAsync();
            var items = await groupedQuery
                .Skip((queryParams.PageNumber - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            var result = new PagedResult<GroupedReportDto>
            {
                Items = items,
                PageNumber = queryParams.PageNumber,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)queryParams.PageSize)
            };

            return Ok(result);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateReportStatusModel model)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
                return NotFound("Báo cáo không tồn tại.");

            report.Status = model.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Trạng thái báo cáo đã được cập nhật." });
        }
    
        [HttpGet("document/{documentId}")]
        public async Task<IActionResult> GetReportsByDocumentId(int documentId)
        {
            var reports = await _context.Reports
                .Include(r => r.User) // Lấy thông tin người báo cáo
                .Where(r => r.DocumentId == documentId)
                .Select(r => new {
                    r.ReportId,
                    r.Reason,
                    r.Details,
                    r.ReportedAt,
                    r.Status,
                    r.ReporterUserId,
                    ReporterName = r.User.FullName, // Trả về tên thay vì cả object User
                    ReporterEmail = r.User.Email
                })
                .OrderByDescending(r => r.ReportedAt)
                .ToListAsync();
    
            if (reports == null || !reports.Any())
            {
                return NotFound("Không tìm thấy báo cáo nào cho tài liệu này.");
            }
    
            return Ok(reports);
        }
    }
    
    public class UpdateReportStatusModel
    {
        [Required]
        public string Status { get; set; } // "Pending", "Resolved", "Rejected"
    }
}