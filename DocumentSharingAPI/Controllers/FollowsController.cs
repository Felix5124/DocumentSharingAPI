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
    public class FollowsController : ControllerBase
    {
        private readonly IFollowRepository _followRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICategoryRepository _categoryRepository;

        public FollowsController(IFollowRepository followRepository, IUserRepository userRepository, ICategoryRepository categoryRepository)
        {
            _followRepository = followRepository;
            _userRepository = userRepository;
            _categoryRepository = categoryRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserFollows()
        {
            var userId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!userId.HasValue)
            {
                return Unauthorized("Không thể xác định người dùng.");
            }
            var follows = await _followRepository.GetByUserIdAsync(userId.Value);
            return Ok(follows);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Follow([FromBody] FollowModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
            {
                return Unauthorized("Vui lòng đăng nhập để thực hiện hành động này.");
            }

            if (model.FollowedUserId == null && model.CategoryId == null)
                return BadRequest("Cần chỉ định người dùng hoặc danh mục để theo dõi.");

            if (model.FollowedUserId.HasValue)
            {
                if (model.FollowedUserId.Value == currentUserId.Value) //Không cho phép tự follow
                    return BadRequest("Bạn không thể tự theo dõi chính mình.");
                var followedUser = await _userRepository.GetByIdAsync(model.FollowedUserId.Value);
                if (followedUser == null)
                    return BadRequest("Người dùng bạn muốn theo dõi không tồn tại.");
            }

            if (model.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(model.CategoryId.Value);
                if (category == null)
                    return BadRequest("Danh mục bạn muốn theo dõi không tồn tại.");
            }

            var existingFollow = await _followRepository.GetFollowAsync(currentUserId.Value, model.FollowedUserId, model.CategoryId);
            if (existingFollow != null)
                return Conflict("Bạn đã theo dõi mục này rồi."); // 409 Conflict

            var follow = new Follow
            {
                UserId = currentUserId.Value,
                FollowedUserId = model.FollowedUserId,
                CategoryId = model.CategoryId,
                FollowedAt = DateTime.Now
            };
            await _followRepository.AddAsync(follow);
            // Trả về thông tin follow vừa tạo
            return CreatedAtAction(nameof(GetUserFollows), new { }, follow);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Unfollow(int followId)
        {
            var follow = await _followRepository.GetByIdAsync(followId);
            if (follow == null)
                return NotFound("Mục theo dõi không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized();

            // Chỉ người tạo follow mới được xóa
            if (follow.UserId != currentUserId.Value)
                return Forbid("Bạn không có quyền hủy theo dõi mục này.");

            await _followRepository.DeleteAsync(followId);
            return NoContent();
        }
    }

    public class FollowModel
    {
        public int? FollowedUserId { get; set; }
        public int? CategoryId { get; set; }
    }
}