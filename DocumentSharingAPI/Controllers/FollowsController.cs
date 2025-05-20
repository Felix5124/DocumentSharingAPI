using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowRepository _followRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository; // Thêm repository cho thông báo

        public FollowsController(
            IFollowRepository followRepository,
            IUserRepository userRepository,
            INotificationRepository notificationRepository) // Inject INotificationRepository
        {
            _followRepository = followRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
        }

        [HttpGet("followers")]
        public async Task<IActionResult> GetUserFollowers([FromQuery] int followedUserId)
        {
            if (followedUserId <= 0)
                return BadRequest("Invalid user ID.");

            try
            {
                var user = await _userRepository.GetByIdAsync(followedUserId);
                if (user == null)
                    return NotFound("User not found.");

                var followers = await _followRepository.GetFollowersByUserIdAsync(followedUserId);
                return Ok(followers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Follow([FromBody] FollowModel model)
        {
            if (model.UserId == null || model.UserId <= 0)
                return BadRequest("Invalid user ID.");

            if (model.FollowedUserId == null || model.FollowedUserId <= 0)
                return BadRequest("Must specify a user to follow.");

            if (model.UserId == model.FollowedUserId)
                return BadRequest("Cannot follow yourself.");

            try
            {
                var user = await _userRepository.GetByIdAsync(model.UserId.Value);
                if (user == null)
                    return NotFound("User not found.");

                var followedUser = await _userRepository.GetByIdAsync(model.FollowedUserId.Value);
                if (followedUser == null)
                    return BadRequest("User to follow not found.");

                var existingFollow = await _followRepository.GetFollowAsync(model.UserId.Value, model.FollowedUserId.Value);
                if (existingFollow != null)
                    return BadRequest("Already following.");

                var follow = new Follow
                {
                    UserId = model.UserId.Value,
                    FollowedUserId = model.FollowedUserId.Value
                };
                await _followRepository.AddAsync(follow);

                // Tạo thông báo cho người dùng B
                var notification = new Notification
                {
                    UserId = model.FollowedUserId.Value, // Người nhận thông báo (B)
                    Message = $"{user.Email} đã bắt đầu theo dõi bạn!", // Nội dung thông báo
                    SentAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);

                return CreatedAtAction(nameof(GetUserFollowers), new { followedUserId = follow.FollowedUserId }, follow);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserFollowing([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound("User not found.");

                var follows = await _followRepository.GetByUserIdAsync(userId);
                return Ok(follows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Unfollow(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid follow ID.");

            try
            {
                var follow = await _followRepository.GetByIdAsync(id);
                if (follow == null)
                    return NotFound($"Follow with ID {id} not found.");

                await _followRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    public class FollowModel
    {
        public int? UserId { get; set; }
        public int? FollowedUserId { get; set; }
    }
}