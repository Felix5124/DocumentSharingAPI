using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class UserDocumentsController : ControllerBase
    {
        private readonly IUserDocumentRepository _userDocumentRepository;
        private readonly AppDbContext _context;

        public UserDocumentsController(IUserDocumentRepository userDocumentRepository, AppDbContext context)
        {
            _userDocumentRepository = userDocumentRepository;
            _context = context;
        }

        [HttpGet("uploads")]
        public async Task<IActionResult> GetUploads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var uploads = await _context.Documents
                .Where(d => d.UploadedBy == userId)
                .Include(d => d.Category)
                .Include(d => d.User)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.Description,
                    d.FileUrl,
                    d.FileType,
                    d.FileSize,
                    d.CoverImageUrl,
                    d.DownloadCount,
                    d.ApprovalStatus,
                    d.IsVipOnly,
                    d.IsLock,
                    d.UploadedBy,
                    d.UploadedAt,
                    d.CategoryId,
                    d.ReportCount,
                    d.ApprovalPriority,
                    Category = new
                    {
                        d.Category.CategoryId,
                        CategoryName = d.Category.Name
                    },
                    Uploader = new
                    {
                        d.User.UserId,
                        d.User.FullName,
                        d.User.AvatarUrl
                    }
                })
                .ToListAsync();

            return Ok(uploads);
        }

        [HttpGet("downloads")]
        public async Task<IActionResult> GetDownloads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var downloads = await _context.UserDocuments
                .Where(ud => ud.UserId == userId && ud.ActionType == "Download")
                .Include(ud => ud.Document)
                    .ThenInclude(d => d.Category)
                .Include(ud => ud.Document)
                    .ThenInclude(d => d.User)
                .OrderByDescending(ud => ud.AddedAt)
                .Select(ud => new
                {
                    ud.Document.DocumentId,
                    ud.Document.Title,
                    ud.Document.Description,
                    ud.Document.FileUrl,
                    ud.Document.FileType,
                    ud.Document.FileSize,
                    ud.Document.CoverImageUrl,
                    ud.Document.DownloadCount,
                    ud.Document.ApprovalStatus,
                    ud.Document.IsVipOnly,
                    ud.Document.IsLock,
                    ud.Document.UploadedBy,
                    ud.Document.UploadedAt,
                    ud.Document.CategoryId,
                    ud.Document.ReportCount,
                    ud.Document.ApprovalPriority,
                    DownloadedAt = ud.AddedAt,
                    AddedAt = ud.AddedAt, // For backward compatibility with web
                    Category = new
                    {
                        ud.Document.Category.CategoryId,
                        CategoryName = ud.Document.Category.Name
                    },
                    Uploader = new
                    {
                        ud.Document.User.UserId,
                        ud.Document.User.FullName,
                        ud.Document.User.AvatarUrl
                    }
                })
                .ToListAsync();

            return Ok(downloads);
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

        // --- THÊM MỚI: Tìm kiếm và phân trang cho Uploads ---
        [HttpGet("search-uploads")]
        public async Task<IActionResult> SearchUserUploads(
            [FromQuery] int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string keyword = "",
            [FromQuery] int? categoryId = null,
            [FromQuery] string sortBy = "newest")
        {
            if (userId <= 0) return BadRequest("Invalid User ID");

            var query = _context.Documents
                .AsNoTracking()
                .Where(d => d.UploadedBy == userId);

            // 1. Lọc
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(d => d.CategoryId == categoryId.Value);
            }

            // 2. Sắp xếp
            query = sortBy.ToLower() switch
            {
                "downloads" => query.OrderByDescending(d => d.DownloadCount),
                "oldest" => query.OrderBy(d => d.UploadedAt),
                "name" => query.OrderBy(d => d.Title),
                _ => query.OrderByDescending(d => d.UploadedAt) // newest
            };

            // 3. Phân trang
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(d => d.Category)
                .Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.Description,
                    d.CoverImageUrl,
                    d.FileType,
                    d.DownloadCount,
                    d.UploadedAt,
                    d.ApprovalStatus,
                    d.IsVipOnly,
                    d.IsLock,
                    CategoryName = d.Category.Name
                })
                .ToListAsync();

            return Ok(new
            {
                data = items,
                total = totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        // --- THÊM MỚI: Tìm kiếm và phân trang cho Downloads ---
        [HttpGet("search-downloads")]
        public async Task<IActionResult> SearchUserDownloads(
            [FromQuery] int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string keyword = "",
            [FromQuery] int? categoryId = null,
            [FromQuery] string sortBy = "newest")
        {
            if (userId <= 0) return BadRequest("Invalid User ID");

            var query = _context.UserDocuments
                .AsNoTracking()
                .Where(ud => ud.UserId == userId && ud.ActionType == "Download")
                .Include(ud => ud.Document)
                .ThenInclude(d => d.Category)
                .Select(ud => ud.Document); // Lấy ra Document từ UserDocument

            // 1. Lọc
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(d => d.CategoryId == categoryId.Value);
            }

            // 2. Sắp xếp (Lưu ý: Sort theo ngày download sẽ phức tạp hơn nếu select Document ngay từ đầu,
            // nhưng ở đây ta sort theo thuộc tính Document cho đồng nhất UI)
            query = sortBy.ToLower() switch
            {
                "downloads" => query.OrderByDescending(d => d.DownloadCount),
                "oldest" => query.OrderBy(d => d.UploadedAt), // Hoặc ud.AddedAt nếu muốn sort theo ngày tải
                "name" => query.OrderBy(d => d.Title),
                _ => query.OrderByDescending(d => d.UploadedAt) // newest
            };

            // 3. Phân trang
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.Description,
                    d.CoverImageUrl,
                    d.FileType,
                    d.DownloadCount,
                    d.UploadedAt, // Hoặc ngày tải nếu join lại
                    d.ApprovalStatus,
                    d.IsVipOnly,
                    d.IsLock,
                    CategoryName = d.Category.Name
                })
                .ToListAsync();

            return Ok(new
            {
                data = items,
                total = totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
    }

    public class AddToLibraryModel
    {
        public int UserId { get; set; }
        public int DocumentId { get; set; }
    }
}