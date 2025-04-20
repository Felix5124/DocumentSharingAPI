using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BadgesController : ControllerBase
    {
        private readonly IBadgeRepository _badgeRepository;

        public BadgesController(IBadgeRepository badgeRepository)
        {
            _badgeRepository = badgeRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var badges = await _badgeRepository.GetAllAsync();
            return Ok(badges);
        }

        [HttpPost]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] BadgeModel model)
        {
            var existingBadge = await _badgeRepository.GetByNameAsync(model.Name);
            if (existingBadge != null)
                return BadRequest("Badge already exists.");

            var badge = new Badge
            {
                Name = model.Name,
                Description = model.Description
            };
            await _badgeRepository.AddAsync(badge);
            return CreatedAtAction(nameof(GetAll), badge);
        }
    }

    public class BadgeModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}