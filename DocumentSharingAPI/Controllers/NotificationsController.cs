using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private const int MaxNotificationsPerUser = 100;

        public NotificationsController(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotifications([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            try
            {
                var notifications = await _notificationRepository.GetByUserIdAsync(userId);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{notificationId}")]
        public async Task<IActionResult> GetNotificationById(int notificationId)
        {
            if (notificationId <= 0)
                return BadRequest("Invalid notification ID.");

            try
            {
                var notification = await _notificationRepository.GetByIdAsync(notificationId);
                if (notification == null)
                    return NotFound($"Notification with ID {notificationId} not found.");

                return Ok(notification);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationModel model)
        {
            if (model.UserId <= 0 || string.IsNullOrEmpty(model.Message))
                return BadRequest("Invalid notification data.");

            try
            {
                // Kiểm tra số lượng thông báo hiện tại của user
                var currentCount = await _notificationRepository.CountByUserIdAsync(model.UserId);
                if (currentCount >= MaxNotificationsPerUser)
                {
                    // Xóa thông báo cũ nhất để giữ số lượng tối đa 5
                    int countToDelete = currentCount - MaxNotificationsPerUser + 1;
                    await _notificationRepository.DeleteOldestByUserIdAsync(model.UserId, countToDelete);
                }

                // Tạo thông báo mới
                var notification = new Notification
                {
                    UserId = model.UserId,
                    Message = model.Message,
                    DocumentId = model.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);
                return CreatedAtAction(nameof(GetUserNotifications), new { userId = notification.UserId }, notification);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            if (notificationId <= 0)
                return BadRequest("Invalid notification ID.");

            try
            {
                var notification = await _notificationRepository.GetByIdAsync(notificationId);
                if (notification == null)
                    return NotFound($"Notification with ID {notificationId} not found.");

                await _notificationRepository.MarkAsReadAsync(notificationId);
                return Ok(new { Message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            if (notificationId <= 0)
                return BadRequest("Invalid notification ID.");

            try
            {
                var notification = await _notificationRepository.GetByIdAsync(notificationId);
                if (notification == null)
                    return NotFound($"Notification with ID {notificationId} not found.");

                await _notificationRepository.DeleteAsync(notificationId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    public class CreateNotificationModel
    {
        public int UserId { get; set; }
        public string Message { get; set; }
        public int? DocumentId { get; set; }
    }
}