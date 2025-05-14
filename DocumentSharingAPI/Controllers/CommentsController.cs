using DocumentSharingAPI.Helpers;
using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentRepository _commentRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;

        public CommentsController(ICommentRepository commentRepository, IDocumentRepository documentRepository, AppDbContext context, IUserRepository userRepository)
        {
            _commentRepository = commentRepository;
            _documentRepository = documentRepository;
            _context = context;
            _userRepository = userRepository;
        }

        [HttpGet("document/{documentId}")]
        public async Task<IActionResult> GetByDocument(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                return NotFound("Tài liệu không tồn tại.");


            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.DocumentId == documentId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new // Tạo DTO để trả về, tránh lỗi cycle và chỉ trả thông tin cần thiết
                {
                    c.CommentId,
                    c.DocumentId,
                    c.Content,
                    c.Rating,
                    c.CreatedAt,
                    c.UserId,
                    UserFullName = c.User.FullName, // Trả về tên thay vì email để thân thiện hơn
                    UserAvatarUrl = c.User.AvatarUrl // Thêm Avatar nếu có
                })
                .ToListAsync();
            return Ok(comments);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CommentModel model)
        {
            if (!ModelState.IsValid) 
                return BadRequest(ModelState);

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) 
                return Unauthorized("Vui lòng đăng nhập để bình luận.");

            var document = await _documentRepository.GetByIdAsync(model.DocumentId);
            if (document == null) 
                return BadRequest("Tài liệu không tồn tại.");

            var user = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (user == null) 
                return BadRequest("Người dùng không hợp lệ."); 

            var comment = new Comment
            {
                DocumentId = model.DocumentId,
                UserId = currentUserId.Value, // Sử dụng UserId từ token
                Content = model.Content,
                Rating = model.Rating,
                CreatedAt = DateTime.Now
            };
            await _commentRepository.AddAsync(comment);

            // Trả về comment vừa tạo với thông tin người dùng
            var createdCommentDto = new
            {
                comment.CommentId,
                comment.DocumentId,
                comment.Content,
                comment.Rating,
                comment.CreatedAt,
                comment.UserId,
                UserFullName = user.FullName,
                UserAvatarUrl = user.AvatarUrl
            };
            return CreatedAtAction(nameof(GetByDocument), new { documentId = comment.DocumentId }, createdCommentDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _commentRepository.GetByIdAsync(id);
            if (comment == null) return NotFound("Bình luận không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized();

            bool isAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            // Chỉ chủ sở hữu comment hoặc Admin mới được xóa
            if (comment.UserId != currentUserId.Value && !isAdmin)
            {
                return Forbid("Bạn không có quyền xóa bình luận này.");
            }

            await _commentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class CommentModel
    {
        public int DocumentId { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; }
        public int? UserId { get; set; } // Thêm UserId vào model
    }
}