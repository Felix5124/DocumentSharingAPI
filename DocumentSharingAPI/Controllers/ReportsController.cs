using DocumentSharingAPI.Models;
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
        public async Task<IActionResult> GetAllReports()
        {
            var reports = await _context.Reports
                .Where(r => r.Status == "Pending") // <-- THÊM DÒNG NÀY
                .Include(r => r.Document)
                .Include(r => r.User)
                .Select(r => new
                {
                    r.ReportId,
                    r.DocumentId,
                    DocumentTitle = r.Document.Title,
                    r.ReporterUserId,
                    ReporterEmail = r.User.Email,
                    ReporterName = r.User.FullName,
                    r.Reason,
                    r.Details,
                    r.Status,
                    r.ReportedAt
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("processed")]
        public async Task<IActionResult> GetProcessedReports()
        {
            var reports = await _context.Reports
                .Include(r => r.Document)
                .Include(r => r.User)
                .Where(r => r.Status == "Resolved" || r.Status == "Rejected")
                .Select(r => new
                {
                    r.ReportId,
                    r.DocumentId,
                    DocumentTitle = r.Document.Title,
                    r.ReporterUserId,
                    ReporterEmail = r.User.Email,
                    ReporterName = r.User.FullName,
                    r.Reason,
                    r.Details,
                    r.Status,
                    r.ReportedAt
                })
                .ToListAsync();

            return Ok(reports);
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
    }

    public class UpdateReportStatusModel
    {
        [Required]
        public string Status { get; set; } // "Pending", "Resolved", "Rejected"
    }
}