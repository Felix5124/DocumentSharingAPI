using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DocumentSharingAPI.Helpers;
using System;


namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostRepository _postRepository;
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;

        public PostsController(IPostRepository postRepository, AppDbContext context, IUserRepository userRepository)
        {
            _postRepository = postRepository;
            _context = context;
            _userRepository = userRepository;
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
        [Authorize]
        public async Task<IActionResult> Create([FromBody] PostModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized("Vui lòng đăng nhập để tạo bài viết.");

            var user = await _userRepository.GetByIdAsync(currentUserId.Value); 
            if (user == null)
                return Unauthorized("Người dùng không hợp lệ.");


            var post = new Post
            {
                Title = model.Title,
                Content = model.Content,
                UserId = currentUserId.Value, // Gán UserId của người tạo
                CreatedAt = DateTime.Now
            };
            await _postRepository.AddAsync(post);
            // Trả về post vừa tạo, có thể kèm thông tin user
            await _context.Entry(post).Reference(p => p.User).LoadAsync(); // Load User nếu cần
            return CreatedAtAction(nameof(GetById), new { id = post.PostId }, post);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _postRepository.GetByIdAsync(id);
            if (post == null)
                return NotFound("Bài viết không tồn tại.");

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized();

            bool isAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            // Chỉ chủ sở hữu bài viết hoặc Admin mới được xóa
            if (post.UserId != currentUserId.Value && !isAdmin) 
                return Forbid("Bạn không có quyền xóa bài viết này.");

            await _postRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class PostModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }
}