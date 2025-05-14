using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DocumentSharingAPI.Helpers;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;

        public NotificationsController(INotificationRepository notificationRepository, IUserRepository userRepository)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserNotifications()
        {
            var userId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!userId.HasValue)
            {
                return Unauthorized("Không thể xác định người dùng.");
            }
            var notifications = await _notificationRepository.GetByUserIdAsync(userId.Value);
            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        //[Authorize]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
                return NotFound("Thông báo không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized();

            // Chỉ user sở hữu notification mới được đánh dấu đã đọc
            if (notification.UserId != currentUserId.Value)
                return Forbid("Bạn không có quyền thực hiện hành động này với thông báo của người khác.");

            if (notification.IsRead)
                return Ok(new { Message = "Thông báo đã được đánh dấu đọc trước đó." });

            await _notificationRepository.MarkAsReadAsync(id);
            return Ok(new { Message = "Thông báo đã được đánh dấu là đã đọc." });
        }
    }
}