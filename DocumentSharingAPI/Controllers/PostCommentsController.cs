using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
            return Ok(comments);
        }

        [HttpPost]
        //[Authorize]
        public async Task<IActionResult> Create([FromBody] PostCommentModel model)
        {
            var post = await _postRepository.GetByIdAsync(model.PostId);
            if (post == null)
                return BadRequest("Post not found.");

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Unauthorized();

            var comment = new PostComment
            {
                PostId = model.PostId,
                UserId = userId,
                Content = model.Content,
                CreatedAt = DateTime.Now
            };
            await _postCommentRepository.AddAsync(comment);
            return CreatedAtAction(nameof(GetByPost), new { postId = comment.PostId }, comment);
        }

        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _postCommentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound();

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            await _postCommentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class PostCommentModel
    {
        public int PostId { get; set; }
        public string Content { get; set; }
    }
}