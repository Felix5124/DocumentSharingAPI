using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using DocumentSharingAPI.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : Controller
    {
        private readonly IGeminiChatService _chatService;
        private readonly IUserRepository _userRepository;
        public ChatbotController(IGeminiChatService chatService, IUserRepository userRepository)
        {
            _chatService = chatService;
            _userRepository = userRepository;
        }

        public class ChatQueryModel
        {
            public string Message { get; set; }
            public int UserId { get; set; } // Frontend will send this
            // Thêm trường này để nhận lịch sử chat từ frontend
            public List<ChatMessageDto>? History { get; set; }
        }

        [HttpPost("query")]
        [Authorize]
        public async Task<IActionResult> PostQuery([FromBody] ChatQueryModel query)
        {
            if (query == null || string.IsNullOrWhiteSpace(query.Message) || query.UserId <= 0)
            {
                return BadRequest(new { message = "Invalid query. Message and UserId are required." });
            }

            // Optional: If you were to pass Firebase UID instead of internal UserId
            var firebaseUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
            {
                return Unauthorized(new { message = "User not authenticated." });
            }
            var user = await _userRepository.GetByFirebaseUidAsync(firebaseUid);
            if (user == null)
            {
                return NotFound(new { message = "User not found in local DB." });
            }
            int internalUserId = user.UserId;

            var response = await _chatService.GetChatbotResponseAsync(query.Message, query.UserId, query.History);
            return Ok(new { reply = response });
        }
    }
}
