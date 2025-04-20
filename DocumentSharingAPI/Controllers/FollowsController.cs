using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
        //[Authorize]
        public async Task<IActionResult> GetUserFollows()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var follows = await _followRepository.GetByUserIdAsync(userId);
            return Ok(follows);
        }

        [HttpPost]
        //[Authorize]
        public async Task<IActionResult> Follow([FromBody] FollowModel model)
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            if (model.FollowedUserId == null && model.CategoryId == null)
                return BadRequest("Must specify user or category to follow.");

            if (model.FollowedUserId != null)
            {
                var followedUser = await _userRepository.GetByIdAsync(model.FollowedUserId.Value);
                if (followedUser == null)
                    return BadRequest("User to follow not found.");
            }

            if (model.CategoryId != null)
            {
                var category = await _categoryRepository.GetByIdAsync(model.CategoryId.Value);
                if (category == null)
                    return BadRequest("Category not found.");
            }

            var existingFollow = await _followRepository.GetFollowAsync(userId, model.FollowedUserId, model.CategoryId);
            if (existingFollow != null)
                return BadRequest("Already following.");

            var follow = new Follow
            {
                UserId = userId,
                FollowedUserId = model.FollowedUserId,
                CategoryId = model.CategoryId,
                FollowedAt = DateTime.Now
            };
            await _followRepository.AddAsync(follow);
            return CreatedAtAction(nameof(GetUserFollows), follow);
        }

        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> Unfollow(int id)
        {
            var follow = await _followRepository.GetByIdAsync(id);
            if (follow == null)
                return NotFound();

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            if (follow.UserId != userId)
                return Forbid();

            await _followRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class FollowModel
    {
        public int? FollowedUserId { get; set; }
        public int? CategoryId { get; set; }
    }
}