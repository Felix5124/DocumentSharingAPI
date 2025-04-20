using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecommendationsController : ControllerBase
    {
        private readonly IRecommendationRepository _recommendationRepository;

        public RecommendationsController(IRecommendationRepository recommendationRepository)
        {
            _recommendationRepository = recommendationRepository;
        }

        [HttpGet]
        //[Authorize]
        public async Task<IActionResult> GetRecommendations()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var documents = await _recommendationRepository.GetRecommendedDocumentsAsync(userId);
            return Ok(documents);
        }
    }
}