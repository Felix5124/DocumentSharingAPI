using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;

        public PostsController(IPostRepository postRepository, AppDbContext context)
        {
            _postRepository = postRepository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postRepository.GetAllWithCommentsAsync();
            return Ok(posts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _postRepository.GetByIdAsync(id);
            if (post == null)
                return NotFound();
            return Ok(post);
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
            return CreatedAtAction(nameof(GetById), new { id = post.PostId }, post);
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
        public string Title { get; set; }
        public string Content { get; set; }
        public int? UserId { get; set; } // UserId bắt buộc từ body
    }
}