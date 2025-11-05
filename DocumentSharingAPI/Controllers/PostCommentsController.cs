using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostCommentsController : ControllerBase
    {
        private readonly IPostCommentRepository _postCommentRepository;
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;
        private readonly IBlobService _blob;

        public PostCommentsController(IPostCommentRepository postCommentRepository, IPostRepository postRepository, AppDbContext context, IBlobService blob)
        {
            _postCommentRepository = postCommentRepository;
            _postRepository = postRepository;
            _context = context;
            _blob = blob;
        }

        private string NormalizeAvatar(string? avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar)) return "default-avatar.png";
            return avatar.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase)
                ? avatar.Substring("avatars/".Length)
                : avatar;
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
                User = c.User != null ? new
                {
                    c.User.Email,
                    c.User.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(c.User.AvatarUrl), TimeSpan.FromHours(1))
                } : null
            }).ToList();
            return Ok(commentDtos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostCommentModel model)
        {
            var post = await _postRepository.GetByIdAsync(model.PostId);
            if (post == null)
                return BadRequest("Bài viết không tồn tại.");

            // Bắt buộc phải có UserId trong body
            if (model.UserId == null || model.UserId <= 0)
                return BadRequest("UserId là bắt buộc.");

            // Kiểm tra UserId có tồn tại trong cơ sở dữ liệu
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return BadRequest("Người dùng không tồn tại.");

            var comment = new PostComment
            {
                PostId = model.PostId,
                UserId = model.UserId.Value,
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
                User = user != null ? new
                {
                    user.Email,
                    user.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(user.AvatarUrl), TimeSpan.FromHours(1))
                } : null
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _postCommentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound();

            await _postCommentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class PostCommentModel
    {
        public int PostId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? UserId { get; set; } // UserId bắt buộc từ body
    }
}