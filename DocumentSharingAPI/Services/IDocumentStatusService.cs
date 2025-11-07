// DocumentSharingAPI/Services/IDocumentStatusService.cs
using System.Threading.Tasks;

namespace DocumentSharingAPI.Services
{
    public interface IDocumentStatusService
    {
        /// <summary>
        /// Kiểm tra tài liệu dựa trên số lượt tải và báo cáo,
        /// và tự động chuyển về trạng thái "Pending" nếu cần.
        /// Đồng thời gửi thông báo cho người dùng và admin.
        /// </summary>
        /// <param name="documentId">ID của tài liệu cần kiểm tra.</param>
        /// <returns>Task</returns>
        Task CheckAndPotentiallyDemoteDocumentAsync(int documentId);
        
        /// <summary>
        /// Kiểm tra tài liệu dựa trên số lượt tải và báo cáo,
        /// và tự động duyệt tài liệu nếu đủ điều kiện.
        /// Đồng thời gửi thông báo cho người đăng.
        /// </summary>
        /// <param name="documentId">ID của tài liệu cần kiểm tra.</param>
        /// <returns>Task</returns>
        Task CheckAndPotentiallyPromoteDocumentAsync(int documentId);
    }
}