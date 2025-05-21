using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
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
                    d.CoverImageUrl,
                    d.UploadedAt,
                    d.DownloadCount,
                    d.FileType,
                    d.PointsRequired,
                    d.IsApproved,
                    d.IsLock, // Thêm trạng thái khóa
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
                document.CoverImageUrl,
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
                document.IsLock, // Thêm trạng thái khóa
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
                IsApproved = false,
                IsLock = false // Mặc định tài liệu mới không bị khóa
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

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
            {
                Console.WriteLine($"Invalid category ID: {model.CategoryId}");
                return BadRequest("Danh mục không hợp lệ.");
            }

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null && existingDocument.DocumentId != id)
            {
                Console.WriteLine($"Document title already exists: {model.Title}");
                return BadRequest("Tiêu đề tài liệu đã tồn tại.");
            }

            document.Title = model.Title ?? document.Title;
            document.Description = model.Description ?? document.Description;
            document.CategoryId = model.CategoryId != 0 ? model.CategoryId : document.CategoryId;
            document.PointsRequired = model.PointsRequired != 0 ? model.PointsRequired : document.PointsRequired;

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                string newCoverPath = await SaveCoverImageAsync(model.CoverImage);
                if (newCoverPath == "INVALID_TYPE")
                {
                    return BadRequest("Định dạng ảnh bìa không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, GIF, TIFF, TIF, HEIC, HEIF.");
                }
                if (!string.IsNullOrEmpty(newCoverPath))
                {
                    // Xoá cái hình cũ nếu cho là default hoặc đã tồn tại
                    if (!string.IsNullOrEmpty(document.CoverImageUrl) && !document.CoverImageUrl.Contains("default-cover"))
                    {
                        var oldCoverFilePath = Path.Combine(Directory.GetCurrentDirectory(), document.CoverImageUrl);
                        if (System.IO.File.Exists(oldCoverFilePath))
                        {
                            try { System.IO.File.Delete(oldCoverFilePath); }
                            catch (Exception ex) { Console.WriteLine($"Error deleting old cover file {oldCoverFilePath}: {ex.Message}"); }
                        }
                    }
                    document.CoverImageUrl = newCoverPath;
                }
            }

            if (model.File != null && model.File.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
                var extension = Path.GetExtension(model.File.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    Console.WriteLine($"Invalid file extension: {extension}");
                    return BadRequest("Định dạng file không hợp lệ. Chỉ chấp nhận PDF, DOCX, và TXT.");
                }

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

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

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
            try
            {
                Console.WriteLine($"Attempting to delete document with ID: {id}");
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    Console.WriteLine($"Document with ID {id} not found.");
                    return NotFound("Tài liệu không tồn tại.");
                }

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
                    else
                    {
                        Console.WriteLine($"File not found: {filePath}");
                    }
                }

                await _documentRepository.DeleteAsync(id);
                Console.WriteLine($"Document {id} deleted successfully.");
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting document {id}: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
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

            // Hình ảnh mặc định khi không có hình ảnh bìa
            string coverImageUrl = "ImageCovers/cat.jpg";
            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                string uploadedCoverPath = await SaveCoverImageAsync(model.CoverImage);
                if (uploadedCoverPath == "INVALID_TYPE")
                {
                    return BadRequest("Định dạng ảnh bìa không hợp lệ. Chỉ chấp nhận JPG, PNG, GIF, TIFF, TIF, HEIC, HEIF.");
                }
                if (!string.IsNullOrEmpty(uploadedCoverPath))
                {
                    coverImageUrl = uploadedCoverPath;
                }
            }

            var document = new Document
            {
                Title = model.Title,
                Description = model.Description,
                FileUrl = $"Files/{fileName}",
                FileType = extension.TrimStart('.'),
                FileSize = model.File.Length,
                CoverImageUrl = coverImageUrl,
                CategoryId = model.CategoryId,
                UploadedBy = model.UploadedBy,
                UploadedAt = DateTime.Now,
                PointsRequired = model.PointsRequired,
                IsApproved = false,
                IsLock = false // Mặc định tài liệu mới không bị khóa
            };
            await _documentRepository.AddAsync(document);
            Console.WriteLine($"Document created with ID: {document.DocumentId}");

            var userDocument = new UserDocument
            {
                UserId = model.UploadedBy,
                DocumentId = document.DocumentId,
                ActionType = "Upload",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument);

            await _userRepository.UpdatePointsAsync(model.UploadedBy, 10);

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
                        d.CoverImageUrl,
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
                        d.IsLock, // Thêm trạng thái khóa
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
                        d.IsApproved,
                        d.IsLock // Thêm trạng thái khóa
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

                if (document.IsLock)
                    return BadRequest(new { message = "Tài liệu đã bị khóa." }); // Kiểm tra trạng thái khóa

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

            if (document.IsLock)
            {
                Console.WriteLine($"Document {id} is locked.");
                return BadRequest("Tài liệu đã bị khóa."); // Kiểm tra trạng thái khóa
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

        [HttpGet("top-downloaded")]
        public async Task<IActionResult> GetTopDownloadedDocument()
        {
            try
            {
                var topDocument = await _documentRepository.GetTopDownloadedDocumentAsync();
                if (topDocument == null)
                    return NotFound("Không có tài liệu nào được tải.");

                return Ok(topDocument);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                // Tổng số người dùng
                var totalUsers = await _context.Users.CountAsync();

                // Tổng số tài liệu
                var totalDocuments = await _context.Documents.CountAsync();

                // Tổng số lượt tải về (đếm số hành động "Download" trong UserDocuments)
                var totalDownloads = await _context.UserDocuments
                    .Where(ud => ud.ActionType == "Download")
                    .CountAsync();

                return Ok(new
                {
                    TotalUsers = totalUsers,
                    TotalDocuments = totalDocuments,
                    TotalDownloads = totalDownloads
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching statistics: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }
        // Thêm endpoint mới: Khóa/Mở khóa tài liệu
        [HttpPut("{id}/lock")]
        public async Task<IActionResult> LockUnlockDocument(int id, [FromBody] LockDocumentModel model)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound("Tài liệu không tồn tại.");

                await _documentRepository.UpdateLockStatusAsync(id, model.IsLocked);

                // Gửi thông báo cho người upload tài liệu
                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được {(model.IsLocked ? "khóa" : "mở khóa")}.",
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
                Console.WriteLine($"Document {id} {(model.IsLocked ? "locked" : "unlocked")} and notification sent.");

                return Ok(new { Message = $"Tài liệu đã được {(model.IsLocked ? "khóa" : "mở khóa")} thành công." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while {(model.IsLocked ? "locking" : "unlocking")} document {id}: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        // Đề xuất tài liệu liên quan
        [HttpGet("{id}/related")]
        public async Task<IActionResult> GetRelatedDocuments(int id, [FromQuery] int count = 4)
        {
            var currentDocument = await _documentRepository.GetByIdAsync(id); 
            if (currentDocument == null || currentDocument.CategoryId == 0)
            {
                return Ok(new List<object>());
            }

            var relatedDocumentsQuery = _context.Documents
                .Where(d => d.CategoryId == currentDocument.CategoryId &&
                            d.DocumentId != id &&
                            d.IsApproved &&
                            !d.IsLock) 
                .OrderByDescending(d => d.DownloadCount) 
                .Take(count);

            var rawRelatedDocs = await relatedDocumentsQuery.ToListAsync();
            var result = new List<object>();

            foreach (var d in rawRelatedDocs)
            {
                var user = await _userRepository.GetByIdAsync(d.UploadedBy);
                result.Add(new
                {
                    d.DocumentId,
                    d.Title,
                    d.CoverImageUrl,
                    UploadedByEmail = user?.Email ?? "Không xác định"
                });
            }

            return Ok(result);
        }

        // Phương thức lưu hình ảnh bìa
        private async Task<string> SaveCoverImageAsync(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".heic", ".heif" };
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!allowedImageExtensions.Contains(extension))
            {
                Console.WriteLine($"Invalid cover image extension: {extension}");
                return "INVALID_TYPE";
            }

            var coversDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ImageCovers");
            if (!Directory.Exists(coversDirectory))
            {
                Directory.CreateDirectory(coversDirectory);
            }

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
            var filePath = Path.Combine(coversDirectory, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"ImageCovers/{fileName}";
        }

    }
    

    public class DocumentModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string FileUrl { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string? CoverImageUrl { get; set; }
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
        public IFormFile? CoverImage { get; set; }
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

    // Thêm model cho khóa/mở khóa tài liệu
    public class LockDocumentModel
    {
        public bool IsLocked { get; set; }
    }
}
