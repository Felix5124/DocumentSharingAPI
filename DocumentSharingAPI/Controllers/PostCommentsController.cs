using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DocumentSharingAPI.Helpers;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostCommentsController : ControllerBase
    {
        private readonly IPostCommentRepository _postCommentRepository;
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;


        public PostCommentsController(IPostCommentRepository postCommentRepository, IPostRepository postRepository, AppDbContext context, IUserRepository userRepository)
        {
            _postCommentRepository = postCommentRepository;
            _postRepository = postRepository;
            _context = context;
            _userRepository = userRepository;
        }

        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetByPost(int postId)
        {
            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound("Bài viết không tồn tại.");

            var comments = await _postCommentRepository.GetByPostIdAsync(postId);
            var commentDtos = comments.Select(c => new
            {
                c.PostCommentId,
                c.PostId,
                c.Content,
                c.CreatedAt,
                c.UserId,
                UserFullName = c.User?.Email, 
                UserAvatarUrl = c.User?.AvatarUrl
            }).ToList();
            return Ok(commentDtos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostCommentModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var post = await _postRepository.GetByIdAsync(model.PostId);
            if (post == null)
                return BadRequest("Bài viết không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized("Vui lòng đăng nhập để bình luận.");

            var user = await _userRepository.GetByIdAsync(currentUserId.Value); // Lấy thông tin user để trả về
            if (user == null) return Unauthorized("Người dùng không hợp lệ.");


            var comment = new PostComment
            {
                PostId = model.PostId,
                UserId = currentUserId.Value, // UserId từ token
                Content = model.Content,
                CreatedAt = DateTime.Now
            };
            await _postCommentRepository.AddAsync(comment);

            // Trả về comment vừa tạo với thông tin người dùng
            var createdCommentDto = new
            {
                comment.PostCommentId,
                comment.PostId,
                comment.Content,
                comment.CreatedAt,
                comment.UserId,
                UserFullName = user.FullName,
                UserAvatarUrl = user.AvatarUrl
            };

            return CreatedAtAction(nameof(GetByPost), new { postId = comment.PostId }, createdCommentDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _postCommentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound("Bình luận không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized();

            bool isAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            // Chỉ chủ sở hữu comment hoặc Admin được xóa
            if (comment.UserId != currentUserId.Value && !isAdmin)
                return Forbid("Bạn không có quyền xóa bình luận này.");

            await _postCommentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class PostCommentModel
    {
        public int PostId { get; set; }
        public string Content { get; set; }
        public int? UserId { get; set; } // Thêm UserId để frontend gửi
    }
}