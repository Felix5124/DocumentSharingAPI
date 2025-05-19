using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostCommentsController : ControllerBase
    {
        private readonly IPostCommentRepository _postCommentRepository;
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;

        public PostCommentsController(IPostCommentRepository postCommentRepository, IPostRepository postRepository, AppDbContext context)
        {
            _postCommentRepository = postCommentRepository;
            _postRepository = postRepository;
            _context = context;
        }

        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetByPost(int postId)
        {
            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound("Post not found.");

            var comments = await _postCommentRepository.GetByPostIdAsync(postId);
            var commentDtos = comments.Select(c => new
            {
                c.PostCommentId,
                c.PostId,
                c.Content,
                c.CreatedAt,
                c.UserId,
                UserEmail = c.User?.Email ?? "Ẩn danh"
            }).ToList();
            return Ok(commentDtos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostCommentModel model)
        {
            var post = await _postRepository.GetByIdAsync(model.PostId);
            if (post == null)
                return BadRequest("Post not found.");

            // Lấy FirebaseUid từ token (nếu có)
            var firebaseUid = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
            {
                // Nếu không có token, yêu cầu UserId từ body
                if (model.UserId == null || model.UserId <= 0)
                    return BadRequest("UserId is required.");
            }

            int userId;
            if (!string.IsNullOrEmpty(firebaseUid))
            {
                // Tìm user dựa trên FirebaseUid
                var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
                if (user == null)
                    return BadRequest("User not found.");

                userId = user.UserId;
            }
            else
            {
                // Nếu không có token, sử dụng UserId từ body
                userId = model.UserId.Value;
            }

            var userCheck = await _context.Users.FindAsync(userId);
            if (userCheck == null)
                return BadRequest("User not found.");

            var comment = new PostComment
            {
                PostId = model.PostId,
                UserId = userId,
                Content = model.Content,
                CreatedAt = DateTime.Now
            };
            await _postCommentRepository.AddAsync(comment);
            return CreatedAtAction(nameof(GetByPost), new { postId = comment.PostId }, new
            {
                comment.PostCommentId,
                comment.PostId,
                comment.Content,
                comment.CreatedAt,
                comment.UserId,
                UserEmail = userCheck.Email
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _postCommentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound();

            // Lấy FirebaseUid từ token (nếu có)
            var firebaseUid = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
                return BadRequest("User authentication required to delete comment.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
            if (user == null || (comment.UserId != user.UserId && !User.IsInRole("Admin")))
                return Forbid("You are not authorized to delete this comment.");

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