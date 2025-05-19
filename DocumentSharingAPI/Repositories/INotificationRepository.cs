using DocumentSharingAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public interface INotificationRepository : IRepository<Notification>
    {
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId);
        Task MarkAsReadAsync(int notificationId);
        new Task DeleteAsync(int notificationId); // Thêm từ khóa new
        Task<int> CountByUserIdAsync(int userId);
        Task DeleteOldestByUserIdAsync(int userId, int countToDelete);
    }
}