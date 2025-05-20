using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class UserDocumentsController : ControllerBase
    {
        private readonly IUserDocumentRepository _userDocumentRepository;

        public UserDocumentsController(IUserDocumentRepository userDocumentRepository)
        {
            _userDocumentRepository = userDocumentRepository;
        }

        [HttpGet("uploads")]
        public async Task<IActionResult> GetUploads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var uploads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Upload");
            return Ok(uploads.Select(ud => new
            {
                ud.DocumentId,
                ud.Document.Title,
                ud.AddedAt,
                ud.Document.DownloadCount
            }));
        }

        [HttpGet("downloads")]
        public async Task<IActionResult> GetDownloads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var downloads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Download");
            return Ok(downloads.Select(ud => new
            {
                ud.DocumentId,
                ud.Document.Title,
                ud.AddedAt
            }));
        }

        [HttpGet("library")]
        public async Task<IActionResult> GetLibrary([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var library = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Library");
            return Ok(library.Select(ud => new
            {
                ud.DocumentId,
                ud.Document.Title,
                ud.AddedAt
            }));
        }

        [HttpPost("library")]
        public async Task<IActionResult> AddToLibrary([FromBody] AddToLibraryModel model)
        {
            if (model.UserId <= 0 || model.DocumentId <= 0)
                return BadRequest("Invalid user ID or document ID.");

            var existing = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(model.UserId, model.DocumentId, "Library");
            if (existing != null)
                return BadRequest("Document already in library.");

            var userDocument = new UserDocument
            {
                UserId = model.UserId,
                DocumentId = model.DocumentId,
                ActionType = "Library",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument);
            return Ok(new { Message = "Added to library" });
        }

        [HttpDelete("library/{documentId}")]
        public async Task<IActionResult> RemoveFromLibrary(int documentId, [FromQuery] int userId)
        {
            if (userId <= 0 || documentId <= 0)
                return BadRequest("Invalid user ID or document ID.");

            var existing = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId, documentId, "Library");
            if (existing == null)
                return NotFound("Document not in library.");

            await _userDocumentRepository.DeleteAsync(documentId);
            return Ok(new { Message = "Removed from library" });
        }
    }

    public class AddToLibraryModel
    {
        public int UserId { get; set; }
        public int DocumentId { get; set; }
    }
}