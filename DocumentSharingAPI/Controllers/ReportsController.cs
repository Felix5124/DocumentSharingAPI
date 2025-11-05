using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
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

        public ReportsController(AppDbContext context, IDocumentRepository documentRepository, INotificationRepository notificationRepository)
        {
            _context = context;
            _documentRepository = documentRepository;
            _notificationRepository = notificationRepository;
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

            // Kiểm tra nếu số lượng report bằng 1/10 số lượng download thì chuyển sang Pending
            if ((document.ApprovalStatus == "SemiApproved" || document.ApprovalStatus == "Approved") &&
                document.DownloadCount > 0 &&
                document.ReportCount >= (document.DownloadCount / 10))
            {
                document.ApprovalStatus = "Pending";
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho admin
                var adminNotification = new Notification
                {
                    UserId = 1, // Giả sử admin có ID = 1
                    Message = $"Tài liệu '{document.Title}' đã bị chuyển sang trạng thái Pending do có {document.ReportCount} báo cáo trên {document.DownloadCount} lượt tải.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(adminNotification);

                // Gửi thông báo cho người đăng
                var uploaderNotification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã bị chuyển sang trạng thái chờ xử lý do có nhiều báo cáo vi phạm.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(uploaderNotification);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Báo cáo của bạn đã được ghi nhận. Cảm ơn bạn đã đóng góp!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllReports()
        {
            var reports = await _context.Reports
                .Include(r => r.Document)
                .Include(r => r.User)
                .Select(r => new
                {
                    r.ReportId,
                    r.DocumentId,
                    DocumentTitle = r.Document.Title,
                    r.ReporterUserId,
                    ReporterEmail = r.User.Email,
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