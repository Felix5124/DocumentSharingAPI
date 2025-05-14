using DocumentSharingAPI.Helpers;
using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserDocumentsController : ControllerBase
    {
        private readonly IUserDocumentRepository _userDocumentRepository;
        private readonly IUserRepository _userRepository; //Lấy UserId
        private readonly AppDbContext _context; //Include Document

        public UserDocumentsController(IUserDocumentRepository userDocumentRepository, IUserRepository userRepository, AppDbContext context)
        {
            _userDocumentRepository = userDocumentRepository;
            _userRepository = userRepository;
            _context = context;
        }

        private async Task<int?> GetCurrentUserId() 
        {
            return await this.GetCurrentUserIdAsync(_userRepository);
        }


        [HttpGet("uploads")]
        public async Task<IActionResult> GetUploads()
        {
            var userId = await GetCurrentUserId();
            if (!userId.HasValue) 
                return Unauthorized();

            var uploads = await _context.UserDocuments
                .Include(ud => ud.Document)
                .Where(ud => ud.UserId == userId.Value && ud.ActionType == "Upload")
                .OrderByDescending(ud => ud.AddedAt)
                .Select(ud => new {
                    // ud.UserDocumentId, // Khóa của UserDocument không có UserDocumentId đơn lẻ
                    ud.DocumentId,
                    ud.Document.Title,
                    ud.AddedAt,
                    ud.Document.DownloadCount,
                    ud.Document.CoverImageUrl,
                    ud.Document.FileType,
                    ud.Document.IsApproved
                })
                .ToListAsync();
            return Ok(uploads);
        }

        [HttpGet("downloads")]
        public async Task<IActionResult> GetDownloads()
        {
            var userId = await GetCurrentUserId();
            if (!userId.HasValue) 
                return Unauthorized();

            var downloads = await _context.UserDocuments
                .Include(ud => ud.Document)
                .Where(ud => ud.UserId == userId.Value && ud.ActionType == "Download")
                .OrderByDescending(ud => ud.AddedAt)
                .Select(ud => new {
                    ud.DocumentId,
                    ud.Document.Title,
                    ud.AddedAt,
                    ud.Document.CoverImageUrl,
                    ud.Document.FileType
                })
                .ToListAsync();
            return Ok(downloads);

        }

        [HttpGet("library")]
        public async Task<IActionResult> GetLibrary()
        {
            var userId = await GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var libraryItems = await _context.UserDocuments
                .Include(ud => ud.Document)
                .Where(ud => ud.UserId == userId.Value && ud.ActionType == "Library")
                .OrderByDescending(ud => ud.AddedAt)
                .Select(ud => new {
                    ud.DocumentId,
                    ud.Document.Title,
                    ud.AddedAt,
                    ud.Document.CoverImageUrl,
                    ud.Document.FileType,
                    ud.Document.Description
                })
                .ToListAsync();
            return Ok(libraryItems);

        }

        [HttpPost("library")]
        public async Task<IActionResult> AddToLibrary([FromBody] AddToLibraryModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = await GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var existing = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId.Value, model.DocumentId, "Library");
            if (existing != null)
                return Conflict("Tài liệu đã có trong thư viện của bạn.");

            var userDocument = new UserDocument
            {
                UserId = userId.Value,
                DocumentId = model.DocumentId,
                ActionType = "Library",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument); 
            return Ok(new { Message = "Đã thêm vào thư viện." });

        }

        [HttpDelete("library/{documentId}")]
        public async Task<IActionResult> RemoveFromLibrary(int documentId)
        {
            var userId = await GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            // Tìm bản ghi UserDocument cụ thể để xóa
            var userDocumentEntry = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId.Value, documentId, "Library");

            if (userDocumentEntry == null)
                return NotFound("Tài liệu không có trong thư viện của bạn.");

            // UserDocument có khóa chính ghép, nên cần truyền entity để xóa
            await _userDocumentRepository.DeleteAsync(userDocumentEntry);
            return Ok(new { Message = "Đã xóa khỏi thư viện." });
        }
    }

    public class AddToLibraryModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public int DocumentId { get; set; }
    }
}