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

        public CommentsController(ICommentRepository commentRepository, IDocumentRepository documentRepository, AppDbContext context)
        {
            _commentRepository = commentRepository;
            _documentRepository = documentRepository;
            _context = context;
        }

        [HttpGet("document/{documentId}")]
        public async Task<IActionResult> GetByDocument(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                return NotFound("Document not found.");

            var comments = await _commentRepository.GetByDocumentIdAsync(documentId);
            var commentDtos = comments.Select(c => new
            {
                c.CommentId,
                c.DocumentId,
                c.Content,
                c.Rating,
                c.CreatedAt,
                c.UserId,
                UserEmail = c.User?.Email ?? "Ẩn danh"
            }).ToList();
            return Ok(commentDtos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CommentModel model)
        {
            var document = await _documentRepository.GetByIdAsync(model.DocumentId);
            if (document == null)
                return BadRequest("Document not found.");

            // Lấy UserId từ body (do frontend gửi)
            if ( model.UserId <= 0)
                return BadRequest("UserId is required.");

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return BadRequest("User not found.");

            var comment = new Comment
            {
                DocumentId = model.DocumentId,
                UserId = (int)model.UserId, // Sử dụng UserId từ body
                Content = model.Content,
                Rating = model.Rating,
                CreatedAt = DateTime.Now
            };
            await _commentRepository.AddAsync(comment);

            // Gán huy hiệu "Top Commenter" nếu người dùng đạt 50 bình luận
            var commentCount = await _context.Comments.CountAsync(c => c.UserId == model.UserId);
            if (commentCount >= 50)
            {
                var badge = await _context.Badges.FirstOrDefaultAsync(b => b.Name == "Top Commenter");
                if (badge == null)
                {
                    badge = new Badge
                    {
                        Name = "Top Commenter",
                        Description = "Awarded for posting 50 comments"
                    };
                    await _context.Badges.AddAsync(badge);
                    await _context.SaveChangesAsync();
                }

                var userBadge = await _context.UserBadges
                    .FirstOrDefaultAsync(ub => ub.UserId == model.UserId && ub.BadgeId == badge.BadgeId);
                if (userBadge == null)
                {
                    userBadge = new UserBadge
                    {
                        UserId = model.UserId,
                        BadgeId = badge.BadgeId,
                        EarnedAt = DateTime.Now
                    };
                    await _context.UserBadges.AddAsync(userBadge);
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByDocument), new { documentId = comment.DocumentId }, new
            {
                comment.CommentId,
                comment.DocumentId,
                comment.Content,
                comment.Rating,
                comment.CreatedAt,
                comment.UserId,
                UserEmail = user.Email
            });
        }
        [HttpGet("count")]
        public async Task<IActionResult> GetCommentCount([FromQuery] int userId)
        {
            Console.WriteLine($"GetCommentCount called with userId: {userId}"); // Thêm log để debug
            if (userId <= 0)
            {
                Console.WriteLine("Invalid user ID: userId <= 0");
                return BadRequest("Invalid user ID.");
            }

            try
            {
                var commentCount = await _context.Comments.CountAsync(c => c.UserId == userId);
                Console.WriteLine($"Comment count for userId {userId}: {commentCount}");
                return Ok(new { commentCount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCommentCount: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _commentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound();

            // Kiểm tra UserId từ body (hoặc bạn có thể bỏ kiểm tra nếu không cần)
            var userId = comment.UserId; // Ở đây có thể lấy từ body nếu frontend gửi
            if (userId <= 0)
                return BadRequest("UserId is required to delete the comment.");

            await _commentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class CommentModel
    {
        public int DocumentId { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; }
        public int UserId { get; set; } // Thêm UserId vào model
    }
}