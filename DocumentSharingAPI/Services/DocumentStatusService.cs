// DocumentSharingAPI/Services/DocumentStatusService.cs
using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Services
{
    public class DocumentStatusService : IDocumentStatusService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;


        public DocumentStatusService(IDocumentRepository documentRepository, INotificationRepository notificationRepository, IUserRepository userRepository)
        {
            _documentRepository = documentRepository;
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
        }

        public async Task CheckAndPotentiallyDemoteDocumentAsync(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null || (document.ApprovalStatus != "SemiApproved" && document.ApprovalStatus != "Approved"))
            {
                // Không cần xử lý nếu tài liệu không tồn tại hoặc đã ở trạng thái Pending/Rejected/Suspended
                return;
            }

            bool shouldChangeToPending = false;

            if (document.ApprovalStatus == "SemiApproved")
            {
                // GIỮ NGUYÊN LOGIC CŨ cho tài liệu chưa được duyệt hoàn toàn
                if ((document.DownloadCount <= 20 && document.ReportCount >= 2) ||
                    (document.DownloadCount > 20 && document.DownloadCount <= 100 && document.ReportCount >= 3) ||
                    (document.DownloadCount > 100 && document.ReportCount >= (document.DownloadCount / 10.0)))
                {
                    shouldChangeToPending = true;
                }
            }
            else if (document.ApprovalStatus == "Approved")
            {
                // === BẮT ĐẦU LOGIC CẢI TIẾN CHO TÀI LIỆU ĐÃ DUYỆT ===

                // 1. Ngưỡng cơ bản là 10 báo cáo
                const int baseReportThreshold = 10;

                const int downloadsPerExtraReport = 10;
                int dynamicThreshold = baseReportThreshold + (document.DownloadCount / downloadsPerExtraReport);
                //vd số download =50 thì 10 + (50 / 10) = 15 (15 report -> pending)
                // So sánh số báo cáo hiện tại với ngưỡng linh động
                if (document.ReportCount >= dynamicThreshold)
                {
                    shouldChangeToPending = true;
                }

                // === KẾT THÚC LOGIC CẢI TIẾN ===
            }

            if (shouldChangeToPending)
            {
                document.ApprovalStatus = "Pending";
                await _documentRepository.UpdateAsync(document);

                var adminUsers = (await _userRepository.GetAllAsync()).Where(u => u.IsAdmin);

                // Gửi thông báo đến từng quản trị viên
                foreach (var admin in adminUsers)
                {
                    var adminNotification = new Notification
                    {
                        UserId = admin.UserId, // Sử dụng UserId của từng admin
                        Message = $"Tài liệu '{document.Title}' đã bị chuyển sang trạng thái Pending do có {document.ReportCount} báo cáo.",
                        DocumentId = document.DocumentId,
                        SentAt = DateTime.Now,
                        IsRead = false
                    };
                    await _notificationRepository.AddAsync(adminNotification);
                }

                // Gửi thông báo cho người đăng
                var uploaderNotification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được chuyển sang trạng thái chờ xử lý do có nhiều báo cáo vi phạm.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(uploaderNotification);
            }
        }

        public async Task CheckAndPotentiallyPromoteDocumentAsync(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);

            // Chỉ xử lý các tài liệu đang ở trạng thái bán duyệt
            if (document == null || document.ApprovalStatus != "SemiApproved")
            {
                return;
            }

            bool shouldBeApproved = false;
            int downloads = document.DownloadCount;
            int reports = document.ReportCount;

            // --- LOGIC LINH HOẠT ---
            // Giai đoạn 1: Dưới 50 lượt tải -> Yêu cầu tuyệt đối không có báo cáo
            if (downloads >= 10 && downloads <= 50)
            {
                if (reports == 0)
                {
                    shouldBeApproved = true;
                }
            }
            // Giai đoạn 2: Từ 51 đến 200 lượt tải -> Chấp nhận 1 báo cáo
            else if (downloads > 50 && downloads <= 200)
            {
                if (reports <= 1)
                {
                    shouldBeApproved = true;
                }
            }
            // Giai đoạn 3: Trên 200 lượt tải -> Xét theo tỷ lệ (ví dụ: tỷ lệ báo cáo < 2%)
            else if (downloads > 200)
            {
                double reportRatio = (double)reports / downloads;
                if (reportRatio < 0.02) // Chấp nhận dưới 2% báo cáo
                {
                    shouldBeApproved = true;
                }
            }
            // --- KẾT THÚC LOGIC LINH HOẠT ---

            if (shouldBeApproved)
            {
                document.ApprovalStatus = "Approved";
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho người đăng
                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được tự động duyệt nhờ tín hiệu tốt từ cộng đồng.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);
            }
        }
    }
}