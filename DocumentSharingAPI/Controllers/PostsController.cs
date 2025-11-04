using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;
        private readonly IBlobService _blob;

        public PostsController(IPostRepository postRepository, AppDbContext context, IBlobService blob)
        {
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

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postRepository.GetAllWithCommentsAsync();
            var dtos = posts.Select(p => new
            {
                p.PostId,
                p.Title,
                p.Content,
                p.CreatedAt,
                p.UserId,
                User = p.User == null ? null : new
                {
                    p.User.Email,
                    p.User.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(p.User.AvatarUrl), TimeSpan.FromHours(1))
                },
                Comments = p.Comments?.Select(c => new
                {
                    c.PostCommentId,
                    c.PostId,
                    c.Content,
                    c.CreatedAt,
                    c.UserId,
                    User = c.User == null ? null : new
                    {
                        c.User.Email,
                        c.User.FullName,
                        AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(c.User.AvatarUrl), TimeSpan.FromHours(1))
                    }
                }).ToList()
            });
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _postRepository.GetByIdAsync(id);
            if (post == null)
                return NotFound();
            var dto = new
            {
                post.PostId,
                post.Title,
                post.Content,
                post.CreatedAt,
                post.UserId,
                User = post.User == null ? null : new
                {
                    post.User.Email,
                    post.User.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(post.User.AvatarUrl), TimeSpan.FromHours(1))
                }
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostModel model)
        {
            // Bắt buộc phải có UserId trong body
            if (model.UserId == null || model.UserId <= 0)
                return BadRequest("UserId là bắt buộc.");

            // Kiểm tra UserId có tồn tại trong cơ sở dữ liệu
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return BadRequest("Người dùng không tồn tại.");

            var post = new Post
            {
                Title = model.Title,
                Content = model.Content,
                UserId = model.UserId.Value,
                CreatedAt = DateTime.Now
            };
            await _postRepository.AddAsync(post);
            var createdUser = await _context.Users.FindAsync(post.UserId);
            var result = new
            {
                post.PostId,
                post.Title,
                post.Content,
                post.CreatedAt,
                post.UserId,
                User = createdUser == null ? null : new
                {
                    createdUser.Email,
                    createdUser.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(createdUser.AvatarUrl), TimeSpan.FromHours(1))
                }
            };
            return CreatedAtAction(nameof(GetById), new { id = post.PostId }, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _postRepository.GetByIdAsync(id);
            if (post == null)
                return NotFound();

            await _postRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class PostModel
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int? UserId { get; set; } // UserId bắt buộc từ body
    }
}