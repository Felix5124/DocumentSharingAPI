using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowRepository _followRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IBlobService _blob;

        public FollowsController(
            IFollowRepository followRepository,
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            IBlobService blob)
        {
            _followRepository = followRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _blob = blob;
        }

        private string NormalizeAvatar(string avatar)
        {
            if (string.IsNullOrEmpty(avatar))
                return "default-avatar.png";
            return avatar.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase)
                ? avatar.Substring("avatars/".Length)
                : avatar;
        }

        // Danh sách người theo dõi (followers)
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

                // Gắn thông tin user (người theo dõi)
                var detailed = followers.Select(f =>
                {
                    var follower = _userRepository.GetByIdAsync(f.UserId).Result;
                    return new
                    {
                        f.FollowId,
                        f.UserId,
                        follower.FullName,
                        follower.Email,
                        AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(follower.AvatarUrl), TimeSpan.FromHours(1))
                    };
                });

                return Ok(detailed);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Danh sách người đang theo dõi (following)
        [HttpGet("following")]
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

                var detailed = follows.Select(f =>
                {
                    var followed = _userRepository.GetByIdAsync(f.FollowedUserId).Result;
                    return new
                    {
                        f.FollowId,
                        UserId = f.FollowedUserId,  // Return the followed user's ID
                        followed.FullName,
                        followed.Email,
                        AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(followed.AvatarUrl), TimeSpan.FromHours(1))
                    };
                });

                return Ok(detailed);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Check if user is following another user
        [HttpGet("check")]
        public async Task<IActionResult> CheckFollowStatus([FromQuery] int userId, [FromQuery] int followedUserId)
        {
            if (userId <= 0 || followedUserId <= 0)
                return BadRequest("Invalid user IDs.");

            try
            {
                var follow = await _followRepository.GetFollowAsync(userId, followedUserId);
                return Ok(new { isFollowing = follow != null, followId = follow?.FollowId });
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

                // Gửi thông báo
                var notification = new Notification
                {
                    UserId = model.FollowedUserId.Value,
                    Message = $"{user.FullName} đã bắt đầu theo dõi bạn!",
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

        // Alternative unfollow by user IDs
        [HttpPost("unfollow")]
        public async Task<IActionResult> UnfollowByUserIds([FromBody] FollowModel model)
        {
            if (model.UserId == null || model.UserId <= 0)
                return BadRequest("Invalid user ID.");
            if (model.FollowedUserId == null || model.FollowedUserId <= 0)
                return BadRequest("Invalid followed user ID.");

            try
            {
                var follow = await _followRepository.GetFollowAsync(model.UserId.Value, model.FollowedUserId.Value);
                if (follow == null)
                    return NotFound("Not following this user.");

                await _followRepository.DeleteAsync(follow.FollowId);
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
