// DocumentSharingAPI/Services/DocumentStatusService.cs
using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using System;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Services
{
    public class DocumentStatusService : IDocumentStatusService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly INotificationRepository _notificationRepository;

        public DocumentStatusService(IDocumentRepository documentRepository, INotificationRepository notificationRepository)
        {
            _documentRepository = documentRepository;
            _notificationRepository = notificationRepository;
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

            // Logic phân tầng chi tiết từ ReportsController
            if (document.DownloadCount == 0 && document.ReportCount >= 2)
            {
                shouldChangeToPending = true;
            }
            else if (document.DownloadCount >= 1 && document.DownloadCount <= 20 && document.ReportCount >= 2)
            {
                shouldChangeToPending = true;
            }
            else if (document.DownloadCount >= 21 && document.DownloadCount <= 100 && document.ReportCount >= 3)
            {
                shouldChangeToPending = true;
            }
            else if (document.DownloadCount > 100 && document.ReportCount >= (document.DownloadCount / 10.0))
            {
                shouldChangeToPending = true;
            }

            if (shouldChangeToPending)
            {
                document.ApprovalStatus = "Pending";
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho admin (giả sử admin có ID = 1)
                var adminNotification = new Notification
                {
                    UserId = 1,
                    Message = $"Tài liệu '{document.Title}' đã bị chuyển sang trạng thái Pending do có {document.ReportCount} báo cáo.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(adminNotification);

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
    }
}