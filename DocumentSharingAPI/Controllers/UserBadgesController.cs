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
    public class UserBadgesController : ControllerBase
    {
        private readonly IUserBadgeRepository _userBadgeRepository;
        private readonly IUserRepository _userRepository;

        public UserBadgesController(IUserBadgeRepository userBadgeRepository, IUserRepository userRepository)
        {
            _userBadgeRepository = userBadgeRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserBadges()
        {
            var userId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!userId.HasValue)
            {
                return Unauthorized("Không thể xác định người dùng.");
            }

            var badges = await _userBadgeRepository.GetByUserIdAsync(userId.Value);
            return Ok(badges);

        }
    }
}