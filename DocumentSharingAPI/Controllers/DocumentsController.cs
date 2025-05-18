using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
        private readonly INotificationRepository _notificationRepository;
        private readonly AppDbContext _context;

        public DocumentsController(
            IDocumentRepository documentRepository,
            ICategoryRepository categoryRepository,
            IUserRepository userRepository,
            IUserDocumentRepository userDocumentRepository,
            INotificationRepository notificationRepository,
            AppDbContext context)
        {
            _documentRepository = documentRepository;
            _categoryRepository = categoryRepository;
            _userRepository = userRepository;
            _userDocumentRepository = userDocumentRepository;
            _notificationRepository = notificationRepository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var documents = await _documentRepository.GetAllAsync();
            var result = new List<object>();

            foreach (var d in documents)
            {
                var user = await _userRepository.GetByIdAsync(d.UploadedBy);
                result.Add(new
                {
                    d.DocumentId,
                    d.Title,
                    d.Description,
                    d.UploadedAt,
                    d.DownloadCount,
                    d.FileType,
                    d.PointsRequired,
                    d.IsApproved,
                    d.UploadedBy,
                    Email = user?.Email ?? "Không xác định"
                });
            }

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {id} not found.");
                return NotFound("Tài liệu không tồn tại.");
            }

            var user = await _userRepository.GetByIdAsync(document.UploadedBy);
            return Ok(new
            {
                document.DocumentId,
                document.Title,
                document.Description,
                document.FileUrl,
                document.FileType,
                document.FileSize,
                document.CategoryId,
                document.Category,
                document.UploadedBy,
                Email = user?.Email ?? "Ẩn danh",
                document.UploadedAt,
                document.DownloadCount,
                document.PointsRequired,
                document.IsApproved,
                document.Comments,
                document.UserDocuments
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DocumentModel model)
        {
            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null)
            {
                Console.WriteLine($"Document title already exists: {model.Title}");
                return BadRequest("Tiêu đề tài liệu đã tồn tại.");
            }

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
            {
                Console.WriteLine($"Invalid category ID: {model.CategoryId}");
                return BadRequest("Danh mục không hợp lệ.");
            }

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
            Console.WriteLine($"Document created with ID: {document.DocumentId}");

            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] UploadDocumentModel model)
        {
            Console.WriteLine("Received update model: " + JsonSerializer.Serialize(model));

            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {id} not found.");
                return NotFound("Tài liệu không tồn tại.");
            }

            // Kiểm tra danh mục
            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
            {
                Console.WriteLine($"Invalid category ID: {model.CategoryId}");
                return BadRequest("Danh mục không hợp lệ.");
            }

            // Kiểm tra tiêu đề trùng lặp (ngoại trừ tài liệu hiện tại)
            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null && existingDocument.DocumentId != id)
            {
                Console.WriteLine($"Document title already exists: {model.Title}");
                return BadRequest("Tiêu đề tài liệu đã tồn tại.");
            }

            // Cập nhật metadata
            document.Title = model.Title ?? document.Title;
            document.Description = model.Description ?? document.Description;
            document.CategoryId = model.CategoryId != 0 ? model.CategoryId : document.CategoryId;
            document.PointsRequired = model.PointsRequired != 0 ? model.PointsRequired : document.PointsRequired;
            // Không cập nhật UploadedBy, giữ nguyên giá trị hiện tại

            // Xử lý file mới nếu có
            if (model.File != null && model.File.Length > 0)
            {
                // Kiểm tra định dạng file
                var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
                var extension = Path.GetExtension(model.File.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    Console.WriteLine($"Invalid file extension: {extension}");
                    return BadRequest("Định dạng file không hợp lệ. Chỉ chấp nhận PDF, DOCX, và TXT.");
                }

                // Xóa file cũ nếu tồn tại
                if (!string.IsNullOrEmpty(document.FileUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                            Console.WriteLine($"Deleted old file: {oldFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old file {oldFilePath}: {ex.Message}");
                        }
                    }
                }

                // Lưu file mới
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                // Cập nhật thông tin file
                document.FileUrl = $"Files/{fileName}";
                document.FileType = extension.TrimStart('.');
                document.FileSize = model.File.Length;
                Console.WriteLine($"Updated file for document {id}: {fileName}");
            }

            await _documentRepository.UpdateAsync(document);
            Console.WriteLine($"Document {id} updated successfully.");

            return Ok(document);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {id} not found.");
                return NotFound("Tài liệu không tồn tại.");
            }

            // Xóa file nếu tồn tại
            if (!string.IsNullOrEmpty(document.FileUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                        Console.WriteLine($"Deleted file: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting file {filePath}: {ex.Message}");
                    }
                }
            }

            await _documentRepository.DeleteAsync(id);
            Console.WriteLine($"Document {id} deleted successfully.");
            return NoContent();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentModel model)
        {
            Console.WriteLine("Received model: " + JsonSerializer.Serialize(model));
            if (model.File == null || model.File.Length == 0)
            {
                Console.WriteLine("No file uploaded.");
                return BadRequest("Không có file được tải lên.");
            }

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
            {
                Console.WriteLine($"Invalid category ID: {model.CategoryId}");
                return BadRequest("Danh mục không hợp lệ.");
            }

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null)
            {
                Console.WriteLine($"Document title already exists: {model.Title}");
                return BadRequest("Tiêu đề tài liệu đã tồn tại.");
            }

            // Kiểm tra định dạng file
            var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
            var extension = Path.GetExtension(model.File.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                Console.WriteLine($"Invalid file extension: {extension}");
                return BadRequest("Định dạng file không hợp lệ. Chỉ chấp nhận PDF, DOCX, và TXT.");
            }

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
                FileType = extension.TrimStart('.'),
                FileSize = model.File.Length,
                CategoryId = model.CategoryId,
                UploadedBy = model.UploadedBy,
                UploadedAt = DateTime.Now,
                PointsRequired = model.PointsRequired,
                IsApproved = false
            };
            await _documentRepository.AddAsync(document);
            Console.WriteLine($"Document created with ID: {document.DocumentId}");

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
                    SentAt = DateTime.Now,
                    IsRead = false
                };

                const int MaxNotificationsPerUser = 100;
                var currentCount = await _notificationRepository.CountByUserIdAsync(follow.UserId);
                if (currentCount >= MaxNotificationsPerUser)
                {
                    int countToDelete = currentCount - MaxNotificationsPerUser + 1;
                    await _notificationRepository.DeleteOldestByUserIdAsync(follow.UserId, countToDelete);
                }

                await _notificationRepository.AddAsync(notification);
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
                    await _context.SaveChangesAsync();
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
            try
            {
                Console.WriteLine($"Received params: Keyword={model.Keyword}, CategoryId={model.CategoryId}, FileType={model.FileType}, SortBy={model.SortBy}, Page={model.Page}, PageSize={model.PageSize}");

                var sortBy = model.SortBy == "UploadAt" ? "UploadedAt" : model.SortBy;

                var (documents, total) = await _documentRepository.GetPagedAsync(
                    model.Page,
                    model.PageSize,
                    string.IsNullOrEmpty(model.Keyword) ? null : model.Keyword,
                    model.CategoryId == 0 ? null : model.CategoryId,
                    string.IsNullOrEmpty(model.FileType) ? null : model.FileType,
                    sortBy
                );

                var result = new List<object>();
                foreach (var d in documents)
                {
                    var user = await _userRepository.GetByIdAsync(d.UploadedBy);
                    result.Add(new
                    {
                        d.DocumentId,
                        d.Title,
                        d.Description,
                        d.FileUrl,
                        d.FileType,
                        d.FileSize,
                        d.CategoryId,
                        d.Category,
                        d.UploadedBy,
                        Email = user?.Email ?? "Không xác định",
                        d.UploadedAt,
                        d.DownloadCount,
                        d.PointsRequired,
                        d.IsApproved,
                        d.Comments,
                        d.UserDocuments
                    });
                }

                return Ok(new { documents = result, total });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
                return BadRequest(new { message = "Lỗi khi tìm kiếm tài liệu: " + ex.Message });
            }
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var documents = await _documentRepository.GetPendingDocumentsAsync();
            return Ok(documents);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    Console.WriteLine($"Document with ID {id} not found.");
                    return NotFound($"Document with ID {id} not found.");
                }

                await _documentRepository.ApproveDocumentAsync(id);

                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được duyệt.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };

                const int MaxNotificationsPerUser = 100;
                var currentCount = await _notificationRepository.CountByUserIdAsync(document.UploadedBy);
                if (currentCount >= MaxNotificationsPerUser)
                {
                    int countToDelete = currentCount - MaxNotificationsPerUser + 1;
                    await _notificationRepository.DeleteOldestByUserIdAsync(document.UploadedBy, countToDelete);
                }

                await _notificationRepository.AddAsync(notification);
                Console.WriteLine($"Document {id} approved and notification sent.");

                return Ok(new { Message = "Document approved and notification sent" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Approve error for document {id}: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("upload-count")]
        public async Task<IActionResult> GetUploadCount([FromQuery] int userId)
        {
            Console.WriteLine($"GetUploadCount called with userId: {userId}");
            if (userId <= 0)
            {
                Console.WriteLine("Invalid user ID: userId <= 0");
                return BadRequest("Invalid user ID.");
            }

            try
            {
                var documents = await _context.Documents
                    .Where(d => d.UploadedBy == userId)
                    .Select(d => new
                    {
                        d.DocumentId,
                        d.Title,
                        d.Description,
                        d.FileType,
                        d.FileSize,
                        d.UploadedAt,
                        d.DownloadCount,
                        d.PointsRequired,
                        d.IsApproved
                    })
                    .ToListAsync();

                var uploadCount = documents.Count;
                Console.WriteLine($"Upload count for userId {userId}: {uploadCount}");
                return Ok(new
                {
                    uploadCount,
                    uploads = documents
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUploadCount: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id, [FromQuery] int userId)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound(new { message = "Tài liệu không tồn tại." });

                if (!document.IsApproved)
                    return BadRequest(new { message = "Tài liệu chưa được duyệt." });

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return BadRequest(new { message = "Người dùng không tồn tại." });

                if (user.Points < document.PointsRequired)
                    return BadRequest(new { message = $"Không đủ điểm để tải tài liệu. Cần {document.PointsRequired} điểm, bạn hiện có {user.Points} điểm." });

                await _userRepository.UpdatePointsAsync(userId, -document.PointsRequired);
                await _documentRepository.IncrementDownloadCountAsync(id);

                var userDocument = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(userId, id, "Download");
                if (userDocument == null)
                {
                    await _userDocumentRepository.AddAsync(new UserDocument
                    {
                        UserId = userId,
                        DocumentId = id,
                        ActionType = "Download",
                        AddedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { message = "Không tìm thấy file." });

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, $"application/{document.FileType}", $"{document.Title}.{document.FileType}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {id} not found.");
                return NotFound("Tài liệu không tồn tại.");
            }

            if (!document.IsApproved)
            {
                Console.WriteLine($"Document {id} is not approved.");
                return BadRequest("Tài liệu chưa được duyệt.");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
            if (!System.IO.File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return NotFound("Không tìm thấy file.");
            }

            if (document.FileType.ToLower() != "pdf")
            {
                Console.WriteLine($"Document {id} is not a PDF.");
                return Ok(new { Message = "Chỉ hỗ trợ xem trước file PDF." });
            }

            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/pdf", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating preview for document {id}: {ex.Message}");
                return BadRequest($"Lỗi khi tạo xem trước: {ex.Message}");
            }
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
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string Title { get; set; }
        public string Description { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Danh mục không được để trống")]
        public int CategoryId { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Người tải lên không được để trống")]
        public int UploadedBy { get; set; }
        public int PointsRequired { get; set; }
        public IFormFile File { get; set; }
    }

    public class SearchDocumentModel
    {
        public string Keyword { get; set; } = "";
        public int CategoryId { get; set; } = 0;
        public string FileType { get; set; } = "";
        public string SortBy { get; set; } = "UploadedAt";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}