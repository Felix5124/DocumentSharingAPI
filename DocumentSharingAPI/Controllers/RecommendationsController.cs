using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DocumentSharingAPI.Helpers;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecommendationsController : ControllerBase
    {
        private readonly IRecommendationRepository _recommendationRepository;
        private readonly IUserRepository _userRepository;

        public RecommendationsController(IRecommendationRepository recommendationRepository, IUserRepository userRepository)
        {
            _recommendationRepository = recommendationRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetRecommendations()
        {
            var userId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!userId.HasValue)
            {
                return Unauthorized("Không thể xác định người dùng.");
            }

            // GetRecommendedDocumentsAsync cần Include User, Category nếu muốn hiển thị thêm thông tin
            var documents = await _recommendationRepository.GetRecommendedDocumentsAsync(userId.Value);
            // Cần tạo DTO phù hợp để trả về thông tin document, tương tự như trong DocumentsController.GetAll
            return Ok(documents);
        }
    }
}