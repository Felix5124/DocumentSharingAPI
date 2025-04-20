using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationsController(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        [HttpGet]
        //[Authorize]
        public async Task<IActionResult> GetUserNotifications()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var notifications = await _notificationRepository.GetByUserIdAsync(userId);
            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        //[Authorize]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
                return NotFound();

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            if (notification.UserId != userId)
                return Forbid();

            await _notificationRepository.MarkAsReadAsync(id);
            return Ok(new { Message = "Notification marked as read" });
        }
    }
}