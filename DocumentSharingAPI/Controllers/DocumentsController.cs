using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Google;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserDocumentRepository _userDocumentRepository;
        private readonly AppDbContext _context;

        public DocumentsController(IDocumentRepository documentRepository, ICategoryRepository categoryRepository, IUserRepository userRepository, AppDbContext context, IUserDocumentRepository userDocumentRepository)
        {
            _documentRepository = documentRepository;
            _categoryRepository = categoryRepository;
            _userRepository = userRepository;
            _context = context;
            _userDocumentRepository = userDocumentRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var documents = await _documentRepository.GetAllAsync();
            return Ok(documents);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();
            return Ok(document);
        }

        [HttpPost]
        //[Authorize]
        public async Task<IActionResult> Create([FromBody] DocumentModel model)
        {
            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null)
                return BadRequest("Document title already exists.");

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
                return BadRequest("Invalid category.");

            var document = new Document
            {
                Title = model.Title,
                Description = model.Description,
                FileUrl = model.FileUrl,
                FileType = model.FileType,
                FileSize = model.FileSize,
                CategoryId = model.CategoryId,
                UploadedBy = model.UploadedBy,
                UploadedAt = DateTime.Now,
                PointsRequired = model.PointsRequired,
                IsApproved = false
            };
            await _documentRepository.AddAsync(document);
            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }

        [HttpPut("{id}")]
        //[Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] DocumentModel model)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
                return BadRequest("Invalid category.");

            document.Title = model.Title ?? document.Title;
            document.Description = model.Description ?? document.Description;
            document.FileUrl = model.FileUrl ?? document.FileUrl;
            document.FileType = model.FileType ?? document.FileType;
            document.FileSize = model.FileSize != 0 ? model.FileSize : document.FileSize;
            document.CategoryId = model.CategoryId != 0 ? model.CategoryId : document.CategoryId;
            document.PointsRequired = model.PointsRequired != 0 ? model.PointsRequired : document.PointsRequired;

            await _documentRepository.UpdateAsync(document);
            return Ok(document);
        }

        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            await _documentRepository.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("upload")]
        //[Authorize]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentModel model)
        {
            if (model.File == null || model.File.Length == 0)
                return BadRequest("No file uploaded.");

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
                return BadRequest("Invalid category.");

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null)
                return BadRequest("Document title already exists.");

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }

            var document = new Document
            {
                Title = model.Title,
                Description = model.Description,
                FileUrl = $"Files/{fileName}",
                FileType = Path.GetExtension(model.File.FileName).ToLower().Replace(".", ""),
                FileSize = model.File.Length,
                CategoryId = model.CategoryId,
                UploadedBy = model.UploadedBy,
                UploadedAt = DateTime.Now,
                PointsRequired = model.PointsRequired,
                IsApproved = false
            };
            await _documentRepository.AddAsync(document);

            // Thêm vào UserDocument với ActionType = Upload
            var userDocument = new UserDocument
            {
                UserId = model.UploadedBy,
                DocumentId = document.DocumentId,
                ActionType = "Upload",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument);

            // Cộng điểm
            await _userRepository.UpdatePointsAsync(model.UploadedBy, 10);

            // Gửi thông báo cho người theo dõi
            var follows = await _context.Follows
                .Where(f => f.FollowedUserId == model.UploadedBy || f.CategoryId == model.CategoryId)
                .ToListAsync();
            foreach (var follow in follows)
            {
                var notification = new Notification
                {
                    UserId = follow.UserId,
                    Message = $"New document '{document.Title}' uploaded in {category.Name}.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now
                };
                await _context.Notifications.AddAsync(notification);
            }

            // Gán huy hiệu
            var uploadCount = await _context.Documents.CountAsync(d => d.UploadedBy == model.UploadedBy);
            if (uploadCount >= 5)
            {
                var badge = await _context.Badges.FirstOrDefaultAsync(b => b.Name == "Uploader");
                if (badge == null)
                {
                    badge = new Badge
                    {
                        Name = "Uploader",
                        Description = "Uploaded 5 documents"
                    };
                    await _context.Badges.AddAsync(badge);
                }

                var userBadge = await _context.UserBadges
                    .FirstOrDefaultAsync(ub => ub.UserId == model.UploadedBy && ub.BadgeId == badge.BadgeId);
                if (userBadge == null)
                {
                    userBadge = new UserBadge
                    {
                        UserId = model.UploadedBy,
                        BadgeId = badge.BadgeId,
                        EarnedAt = DateTime.Now
                    };
                    await _context.UserBadges.AddAsync(userBadge);
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] SearchDocumentModel model)
        {
            var (documents, total) = await _documentRepository.GetPagedAsync(
                model.Page ?? 1,
                model.PageSize ?? 10,
                model.Keyword,
                model.CategoryId,
                model.FileType,
                model.SortBy
            );
            return Ok(new { Documents = documents, Total = total });
        }

        [HttpGet("pending")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPending()
        {
            var documents = await _documentRepository.GetPendingDocumentsAsync();
            return Ok(documents);
        }

        [HttpPut("{id}/approve")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            await _documentRepository.ApproveDocumentAsync(id);
            return Ok(new { Message = "Document approved" });
        }

        [HttpGet("{id}/download")]
        //[Authorize]
        public async Task<IActionResult> Download(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            if (!document.IsApproved)
                return BadRequest("Document is not approved.");

            // Kiểm tra điểm người dùng
            var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0"); // Lấy userId từ JWT
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Unauthorized();

            if (user.Points < document.PointsRequired)
                return BadRequest("Insufficient points.");

            // Trừ điểm
            await _userRepository.UpdatePointsAsync(userId, -document.PointsRequired);

            // Tăng lượt tải
            await _documentRepository.IncrementDownloadCountAsync(id);

            // Thêm vào UserDocument với ActionType = Download
            var userDocument = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId, id, "Download");
            if (userDocument == null)
            {
                userDocument = new UserDocument
                {
                    UserId = userId,
                    DocumentId = id,
                    ActionType = "Download",
                    AddedAt = DateTime.Now
                };
                await _userDocumentRepository.AddAsync(userDocument);
            }

            // Ghi nhận vào Recommendation
            var recommendation = new Recommendation
            {
                UserId = userId,
                DocumentId = id,
                InteractedAt = DateTime.Now
            };
            await _context.Recommendations.AddAsync(recommendation);
            await _context.SaveChangesAsync();

            // Trả file
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, $"application/{document.FileType}", $"{document.Title}.{document.FileType}");
        }

        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            if (!document.IsApproved)
                return BadRequest("Document is not approved.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            if (document.FileType.ToLower() != "pdf")
                return Ok(new { Message = "Preview only available for PDF files." });

            // Đọc trang đầu PDF
            string previewText = "";
            try
            {
                using (var pdfReader = new PdfReader(filePath))
                using (var pdfDocument = new PdfDocument(pdfReader))
                {
                    var page = pdfDocument.GetPage(1);
                    var text = PdfTextExtractor.GetTextFromPage(page);
                    previewText = text.Length > 500 ? text.Substring(0, 500) + "..." : text;
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error generating preview: {ex.Message}");
            }

            return Ok(new { Title = document.Title, PreviewText = previewText });
        }
    }

    public class DocumentModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string FileUrl { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int CategoryId { get; set; }
        public int UploadedBy { get; set; }
        public int PointsRequired { get; set; }
    }

    public class UploadDocumentModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public int UploadedBy { get; set; }
        public int PointsRequired { get; set; }
        public IFormFile File { get; set; }
    }

    public class SearchDocumentModel
    {
        public string Keyword { get; set; }
        public int? CategoryId { get; set; }
        public string FileType { get; set; }
        public string SortBy { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}