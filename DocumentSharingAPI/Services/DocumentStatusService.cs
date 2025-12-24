using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _context;

        public DocumentStatusService(IDocumentRepository documentRepository, INotificationRepository notificationRepository, IUserRepository userRepository, AppDbContext context)
        {
            _documentRepository = documentRepository;
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _context = context;
        }

        // --- LOGIC HẠ CẤP (DEMOTE: SemiApproved/Approved -> Pending) ---
        public async Task CheckAndPotentiallyDemoteDocumentAsync(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            
            // Chỉ xử lý tài liệu đang hoạt động (SemiApproved hoặc Approved)
            if (document == null || (document.ApprovalStatus != "SemiApproved" && document.ApprovalStatus != "Approved"))
            {
                return;
            }

            int reportCount = document.ReportCount;
            bool isVip = document.IsVipOnly;
            int threshold = int.MaxValue;

            // Thiết lập ngưỡng dựa trên trạng thái và loại tài liệu
            if (document.ApprovalStatus == "SemiApproved")
            {
                // Tài liệu Premium chưa kiểm duyệt: 3 báo cáo, Thường: 2 báo cáo
                threshold = isVip ? 3 : 2;
            }
            else if (document.ApprovalStatus == "Approved")
            {
                // Tài liệu Premium đã kiểm duyệt: 5 báo cáo, Thường: 4 báo cáo
                threshold = isVip ? 5 : 4;
            }

            // Kiểm tra điều kiện hạ cấp
            if (reportCount >= threshold)
            {
                document.ApprovalStatus = "Pending";
                // Có thể set IsLock = true nếu muốn khóa ngay lập tức, ở đây ta về Pending để admin duyệt lại
                // document.IsLock = true;
                
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho Admin
                var adminUsers = (await _userRepository.GetAllAsync()).Where(u => u.IsAdmin);
                foreach (var admin in adminUsers)
                {
                    var adminNotification = new Notification
                    {
                        UserId = admin.UserId,
                        Message = $"Cảnh báo: Tài liệu '{document.Title}' (Loại: {(isVip ? "VIP" : "Thường")}) đã nhận {reportCount} báo cáo. Đã chuyển về trạng thái Chờ duyệt (Pending).",
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
                    Message = $"Tài liệu '{document.Title}' của bạn bị tạm ngưng hiển thị do nhận nhiều báo cáo vi phạm. Quản trị viên sẽ xem xét lại.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(uploaderNotification);
            }
        }

        // --- LOGIC THĂNG CẤP (PROMOTE: SemiApproved -> Approved) ---
        public async Task CheckAndPotentiallyPromoteDocumentAsync(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);

            // Chỉ xử lý tài liệu đang ở trạng thái "Chưa kiểm duyệt" (SemiApproved)
            if (document == null || document.ApprovalStatus != "SemiApproved")
            {
                return;
            }

            // Đếm số lượt tải duy nhất (Unique user downloads)
            // Lưu ý: Logic này đã loại trừ người upload tự tải trong Controller hoặc Repository nếu được cài đặt đúng,
            // nhưng truy vấn này đếm tất cả user unique trong bảng UserDocuments action "Download".
            int uniqueDownloads = await _context.UserDocuments
                .Where(ud => ud.DocumentId == documentId && ud.ActionType == "Download")
                .Select(ud => ud.UserId)
                .Distinct()
                .CountAsync();

            // Logic mới: Chỉ cần >= 5 lượt tải duy nhất là duyệt
            if (uniqueDownloads >= 5)
            {
                document.ApprovalStatus = "Approved";
                // Reset report count khi được thăng cấp để công bằng cho trạng thái mới
                document.ReportCount = 0;
                
                await _documentRepository.UpdateAsync(document);

                // Tặng bonus download
                bool isVipBonus = document.IsVipOnly;
                await _userRepository.AddBonusDownloadAsync(document.UploadedBy, isVipBonus);

                // Gửi thông báo chúc mừng
                string bonusType = isVipBonus ? "Premium" : "thường";
                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Chúc mừng! Tài liệu '{document.Title}' đã đạt 5 lượt tải tin cậy và chính thức được Duyệt (Approved). Bạn nhận được 1 lượt tải {bonusType} bonus!",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);
            }
        }
    }
}