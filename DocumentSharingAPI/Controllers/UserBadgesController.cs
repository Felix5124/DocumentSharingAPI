using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
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
        //[Authorize]
        public async Task<IActionResult> GetUserBadges()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var badges = await _userBadgeRepository.GetByUserIdAsync(userId);
            return Ok(badges);
        }
    }
}