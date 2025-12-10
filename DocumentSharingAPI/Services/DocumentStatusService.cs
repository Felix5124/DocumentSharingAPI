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

            bool shouldChangeToPending = false;
            int reports = document.ReportCount;

            // Lấy số người dùng thực tế đã tải (tránh 1 người tải nhiều lần làm loãng tỉ lệ)
            int uniqueDownloads = await _context.UserDocuments
                .Where(ud => ud.DocumentId == documentId && ud.ActionType == "Download")
                .Select(ud => ud.UserId)
                .Distinct()
                .CountAsync();

            // Tránh chia cho 0, mặc định là 1 nếu chưa có ai tải (để tính toán không bị lỗi)
            if (uniqueDownloads == 0) uniqueDownloads = 1; 
            
            double reportRatio = (double)reports / uniqueDownloads;

            if (document.ApprovalStatus == "SemiApproved")
            {
                // Logic cho tài liệu "Chưa kiểm duyệt": Nghiêm ngặt hơn
                // 1. Giai đoạn ít tải (< 20): Chỉ cần 3 báo cáo là gỡ để an toàn.
                if (uniqueDownloads < 20 && reports >= 3)
                {
                    shouldChangeToPending = true;
                }
                // 2. Giai đoạn nhiều tải (>= 20): Nếu tỷ lệ báo cáo >= 15% -> Gỡ
                else if (uniqueDownloads >= 20 && reportRatio > 0.15)
                {
                    shouldChangeToPending = true;
                }
            }
            else if (document.ApprovalStatus == "Approved")
            {
                // Logic cho tài liệu "ĐÃ DUYỆT": Nới lỏng hơn (để tránh bị spam report phá hoại)
                // Điều kiện: tỷ lệ báo cáo >= 15%
                if (reports >= 8 && reportRatio >= 0.20)
                {
                    shouldChangeToPending = true;
                }
            }

            if (shouldChangeToPending)
            {
                document.ApprovalStatus = "Pending";
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho Admin
                var adminUsers = (await _userRepository.GetAllAsync()).Where(u => u.IsAdmin);
                foreach (var admin in adminUsers)
                {
                    var adminNotification = new Notification
                    {
                        UserId = admin.UserId,
                        Message = $"Cảnh báo: Tài liệu '{document.Title}' có tỷ lệ báo cáo cao ({reports} reports/{uniqueDownloads} downloads). Đã chuyển về Pending.",
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
                    Message = $"Tài liệu '{document.Title}' của bạn bị tạm khóa do nhận nhiều báo cáo vi phạm từ cộng đồng.",
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

            bool shouldBeApproved = false;
            
            int uniqueDownloads = await _context.UserDocuments
                .Where(ud => ud.DocumentId == documentId && ud.ActionType == "Download")
                .Select(ud => ud.UserId)
                .Distinct()
                .CountAsync();

            int reports = document.ReportCount;

            // === LOGIC DUYỆT DỰA TRÊN PHẦN TRĂM (%) ===
            
            // Điều kiện tiên quyết: Phải có ít nhất 40 lượt tải để dữ liệu đáng tin cậy.
            if (uniqueDownloads >= 2)
            {
                double reportRatio = (double)reports / uniqueDownloads;

                // Ngưỡng duyệt: Tỷ lệ báo cáo <= 10% (0.1)
                // Cho phép sai số nhỏ (bấm nhầm report)
                if (reportRatio <= 0.10)
                {
                    shouldBeApproved = true;
                }
            }

            if (shouldBeApproved)
            {
                document.ApprovalStatus = "Approved";
                document.ReportCount = 0; // Reset report count khi tự động duyệt
                await _documentRepository.UpdateAsync(document);

                // Tặng bonus download cho người upload (VIP bonus nếu tài liệu là VIP, thường nếu tài liệu thường)
                bool isVipBonus = document.IsVipOnly;
                await _userRepository.AddBonusDownloadAsync(document.UploadedBy, isVipBonus);

                // Gửi thông báo chúc mừng với thông tin bonus
                string bonusType = isVipBonus ? "Premium" : "thường";
                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Chúc mừng! Tài liệu '{document.Title}' đã đạt độ tin cậy cao ({uniqueDownloads} lượt tải, tỷ lệ báo cáo thấp) và chính thức được Duyệt. Bạn đã nhận được 1 lượt tải {bonusType} bonus!",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);
            }
        }
    }
}