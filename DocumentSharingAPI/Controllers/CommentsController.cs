using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            return Ok(comments);
        }

        [HttpPost]
        //[Authorize]
        public async Task<IActionResult> Create([FromBody] CommentModel model)
        {
            var document = await _documentRepository.GetByIdAsync(model.DocumentId);
            if (document == null)
                return BadRequest("Document not found.");

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Unauthorized();

            var comment = new Comment
            {
                DocumentId = model.DocumentId,
                UserId = userId,
                Content = model.Content,
                Rating = model.Rating,
                CreatedAt = DateTime.Now
            };
            await _commentRepository.AddAsync(comment);
            return CreatedAtAction(nameof(GetByDocument), new { documentId = comment.DocumentId }, comment);
        }

        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _commentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound();

            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            await _commentRepository.DeleteAsync(id);
            return NoContent();
        }
    }

    public class CommentModel
    {
        public int DocumentId { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; }
    }
}