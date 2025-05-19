using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserBadgesController : ControllerBase
    {
        private readonly IUserBadgeRepository _userBadgeRepository;

        public UserBadgesController(IUserBadgeRepository userBadgeRepository)
        {
            _userBadgeRepository = userBadgeRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserBadges([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            try
            {
                var badges = await _userBadgeRepository.GetByUserIdAsync(userId);
                return Ok(badges);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}