using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public class NotificationRepository : Repository<Notification>, INotificationRepository
    {
        public NotificationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Notification>> GetByUserIdAsync(int userId)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .Include(n => n.Document)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching notifications for user {userId}: {ex.Message}", ex);
            }
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification == null)
                    throw new Exception($"Notification with ID {notificationId} not found");

                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error marking notification {notificationId} as read: {ex.Message}", ex);
            }
        }

        public new async Task DeleteAsync(int notificationId) // Thêm từ khóa new
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification == null)
                    throw new Exception($"Notification with ID {notificationId} not found");

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting notification {notificationId}: {ex.Message}", ex);
            }
        }

        public async Task<int> CountByUserIdAsync(int userId)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error counting notifications for user {userId}: {ex.Message}", ex);
            }
        }

        public async Task DeleteOldestByUserIdAsync(int userId, int countToDelete)
        {
            try
            {
                var oldestNotifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderBy(n => n.SentAt)
                    .Take(countToDelete)
                    .ToListAsync();

                if (oldestNotifications.Any())
                {
                    _context.Notifications.RemoveRange(oldestNotifications);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting oldest notifications for user {userId}: {ex.Message}", ex);
            }
        }
    }
}