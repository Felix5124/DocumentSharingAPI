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
        public async Task<IActionResult> GetUploads()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
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
        public async Task<IActionResult> GetDownloads()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var downloads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Download");
            return Ok(downloads.Select(ud => new
            {
                ud.DocumentId,
                ud.Document.Title,
                ud.AddedAt
            }));
        }

        [HttpGet("library")]
        public async Task<IActionResult> GetLibrary()
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
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
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var existing = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId, model.DocumentId, "Library");
            if (existing != null)
                return BadRequest("Document already in library.");

            var userDocument = new UserDocument
            {
                UserId = userId,
                DocumentId = model.DocumentId,
                ActionType = "Library",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument);
            return Ok(new { Message = "Added to library" });
        }

        [HttpDelete("library/{documentId}")]
        public async Task<IActionResult> RemoveFromLibrary(int documentId)
        {
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var existing = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId, documentId, "Library");
            if (existing == null)
                return NotFound("Document not in library.");

            await _userDocumentRepository.DeleteAsync(documentId);
            return Ok(new { Message = "Removed from library" });
        }
    }

    public class AddToLibraryModel
    {
        public int DocumentId { get; set; }
    }
}