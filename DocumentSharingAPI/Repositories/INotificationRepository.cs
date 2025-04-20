using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface INotificationRepository : IRepository<Notification>
    {
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId);
        Task MarkAsReadAsync(int id);
    }
}