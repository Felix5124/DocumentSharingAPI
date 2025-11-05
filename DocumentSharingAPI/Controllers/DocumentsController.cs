using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentSharingAPI.Models.DTO;

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
        private readonly ITagRepository _tagRepository;
        private readonly IFollowRepository _followRepository;
        private readonly AppDbContext _context;
        private readonly IBlobService _blob;
        private readonly IFileValidationService _fileValidationService;

        public DocumentsController(
            IDocumentRepository documentRepository,
            ICategoryRepository categoryRepository,
            IUserRepository userRepository,
            IUserDocumentRepository userDocumentRepository,
            INotificationRepository notificationRepository,
            ITagRepository tagRepository,
            IFollowRepository followRepository,
            AppDbContext context,
            IBlobService blob,
            IFileValidationService fileValidationService)
        {
            _documentRepository = documentRepository;
            _categoryRepository = categoryRepository;
            _userRepository = userRepository;
            _userDocumentRepository = userDocumentRepository;
            _notificationRepository = notificationRepository;
            _tagRepository = tagRepository;
            _followRepository = followRepository;
            _context = context;
            _blob = blob;
            _fileValidationService = fileValidationService;
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
                    Tags = d.DocumentTags.Where(dt => dt.Tag != null).Select(dt => new TagDto { TagId = dt.Tag.TagId, Name = dt.Tag.Name }).ToList(),
                    d.Description,
                    d.CoverImageUrl,
                    d.UploadedAt,
                    d.DownloadCount,
                    d.FileType,
                    d.IsVipOnly,
                    ApprovalStatus = d.ApprovalStatus,
                    ReportCount = d.ReportCount,
                    d.IsLock,
                    d.UploadedBy,
                    Email = user?.Email ?? "Không xác định"
                });
            }

            return Ok(result);
        }

        [HttpGet("{id:int}")]
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
                Tags = document.DocumentTags.Where(dt => dt.Tag != null).Select(dt => new TagDto { TagId = dt.Tag.TagId, Name = dt.Tag.Name }).ToList(),

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
                document.IsVipOnly,
                ApprovalStatus = document.ApprovalStatus,
                ReportCount = document.ReportCount,
                document.IsLock,
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
                IsVipOnly = model.IsVipOnly,
                ApprovalStatus = "Pending",
                IsLock = false
            };
            await _documentRepository.AddAsync(document);
            Console.WriteLine($"Document created with ID: {document.DocumentId}");

            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateDocumentDto model)
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
            document.IsVipOnly = model.IsVipOnly;

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".heic", ".heif" };
                var coverExtension = Path.GetExtension(model.CoverImage.FileName).ToLowerInvariant();

                if (!allowedImageExtensions.Contains(coverExtension))
                {
                    return BadRequest("Định dạng ảnh bìa không hợp lệ.");
                }

                // Xóa ảnh cũ nếu có và không phải ảnh mặc định
                if (!string.IsNullOrEmpty(document.CoverImageUrl) &&
                    !document.CoverImageUrl.EndsWith("default-file.png", StringComparison.OrdinalIgnoreCase))
                {
                    await _blob.DeleteAsync("covers", document.CoverImageUrl.Replace("covers/", ""));
                }

                // Upload ảnh mới
                var newGuid = Guid.NewGuid().ToString("N");
                var newFileName = $"{newGuid}{coverExtension}";
                await using var coverStream = model.CoverImage.OpenReadStream();
                await _blob.UploadAsync("covers", newFileName, coverStream, model.CoverImage.ContentType);
                document.CoverImageUrl = $"covers/{newFileName}";
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

                // Xóa file cũ trên Azure Blob
                if (!string.IsNullOrEmpty(document.FileUrl))
                {
                    // Remove prefix "documents/" if present để tránh double path
                    var blobPathToDelete = document.FileUrl.StartsWith("documents/")
                        ? document.FileUrl.Substring("documents/".Length)
                        : document.FileUrl;
                    await _blob.DeleteAsync("documents", blobPathToDelete);
                    Console.WriteLine($"Deleted old blob: {document.FileUrl}");
                }

                // Upload file mới lên Azure Blob
                var newGuid = Guid.NewGuid().ToString("N");
                var newBlobName = $"documents/{newGuid}/{Path.GetFileName(model.File.FileName)}";
                await using var fileStream = model.File.OpenReadStream();

                // Ensure correct MIME type for the updated file
                var correctMimeType = GetMimeTypeByExtension(extension);
                await _blob.UploadAsync("documents", newBlobName, fileStream, correctMimeType);

                document.FileUrl = newBlobName;
                document.FileType = extension.TrimStart('.');
                document.FileSize = model.File.Length;
                Console.WriteLine($"Updated file for document {id}: {newBlobName}");
            }

            if (model.Tags != null) // Nếu model.Tags là null, nghĩa là client không muốn thay đổi tags
            {
                // Lấy các tag IDs hiện tại của document
                var currentTagIds = document.DocumentTags.Select(dt => dt.TagId).ToList();
                var newTagObjects = new List<Tag>();

                // Tạo hoặc lấy các tag objects từ tên tag mới
                foreach (var tagName in model.Tags.Where(tn => !string.IsNullOrWhiteSpace(tn)).Distinct())
                {
                    var tag = await _tagRepository.GetOrCreateTagAsync(tagName);
                    if (tag != null)
                    {
                        newTagObjects.Add(tag);
                    }
                }
                var newTagIds = newTagObjects.Select(t => t.TagId).ToList();

                // Xóa các DocumentTag không còn trong danh sách mới
                var tagsToRemove = document.DocumentTags
                                        .Where(dt => !newTagIds.Contains(dt.TagId))
                                        .ToList();
                if (tagsToRemove.Any())
                {
                    _context.DocumentTags.RemoveRange(tagsToRemove);
                }

                // Thêm các DocumentTag mới
                foreach (var tagObj in newTagObjects)
                {
                    if (!currentTagIds.Contains(tagObj.TagId)) // Chỉ thêm nếu chưa có
                    {
                        _context.DocumentTags.Add(new DocumentTag { DocumentId = document.DocumentId, TagId = tagObj.TagId });
                    }
                }

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

                // Xóa file tài liệu trên Azure Blob
                if (!string.IsNullOrEmpty(document.FileUrl))
                {
                    // Remove prefix "documents/" if present để tránh double path
                    var blobPathToDelete = document.FileUrl.StartsWith("documents/")
                        ? document.FileUrl.Substring("documents/".Length)
                        : document.FileUrl;
                    await _blob.DeleteAsync("documents", blobPathToDelete);
                    Console.WriteLine($"Deleted document blob: {document.FileUrl}");
                }

                if (!string.IsNullOrEmpty(document.CoverImageUrl) &&
    !document.CoverImageUrl.EndsWith("default-file.png", StringComparison.OrdinalIgnoreCase))
                {
                    await _blob.DeleteAsync("covers", document.CoverImageUrl.Replace("covers/", ""));
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

            var user = await _userRepository.GetByIdAsync(model.UploadedBy);
            if (user == null)
            {
                Console.WriteLine($"Invalid user ID: {model.UploadedBy}");
                return BadRequest("Người dùng không hợp lệ.");
            }

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null)
            {
                Console.WriteLine($"Document title already exists: {model.Title}");
                return BadRequest("Tiêu đề tài liệu đã tồn tại.");
            }

            // --- BẮT ĐẦU LOGIC VALIDATION MỚI ---

            // 1. Kiểm tra kích thước file (ví dụ: 50MB)
            const long maxFileSize = 50 * 1024 * 1024; // 50 MB
            if (model.File.Length > maxFileSize)
            {
                return BadRequest($"Dung lượng file không được vượt quá {maxFileSize / 1024 / 1024}MB.");
            }

            // 2. Kiểm tra định dạng file (đuôi file)
            var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".pptx", ".zip" };
            var extension = Path.GetExtension(model.File.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                Console.WriteLine($"Invalid file extension: {extension}");
                return BadRequest("Định dạng file không hợp lệ. Chỉ chấp nhận PDF, DOCX, TXT, PPTX, ZIP.");
            }

            // 3. Kiểm tra chữ ký file (MIME type an toàn hơn)
            await using var fileStreamForValidation = model.File.OpenReadStream();
            var isSignatureValid = await _fileValidationService.ValidateFileSignatureAsync(fileStreamForValidation, extension);
            if (!isSignatureValid)
            {
                return BadRequest("Nội dung file không khớp với định dạng. File có thể bị lỗi hoặc không an toàn.");
            }

            // --- KẾT THÚC LOGIC VALIDATION MỚI ---

            // Upload file tài liệu lên Azure Blob
            var fileGuid = Guid.NewGuid().ToString("N");
            await using var fileStream = model.File.OpenReadStream();
            var fileName = $"{fileGuid}/{Path.GetFileName(model.File.FileName)}";

            // Ensure correct MIME type based on extension
            var correctMimeType = GetMimeTypeByExtension(extension);
            await _blob.UploadAsync("documents", fileName, fileStream, correctMimeType);
            var blobName = $"documents/{fileName}";


            // Upload ảnh bìa lên Azure Blob
            string coverImageUrl = null;
            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".heic", ".heif" };
                var coverExtension = Path.GetExtension(model.CoverImage.FileName).ToLowerInvariant();

                if (!allowedImageExtensions.Contains(coverExtension))
                {
                    return BadRequest("Định dạng ảnh bìa không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, GIF, TIFF, TIF, HEIC, HEIF.");
                }

                // Chỉ truyền tên file, KHÔNG thêm "covers/" ở blobName
                var coverName = $"{fileGuid}{coverExtension}";
                await using var coverStream = model.CoverImage.OpenReadStream();
                await _blob.UploadAsync("covers", coverName, coverStream, model.CoverImage.ContentType);

                // Lưu vào DB với prefix "covers/"
                coverImageUrl = $"covers/{coverName}";

            }
            else
            {
                // Nếu user không upload ảnh, gán ảnh mặc định
                coverImageUrl = "covers/default-cover.png";
            }

            var document = new Document
            {
                Title = model.Title,
                Description = model.Description,
                FileUrl = blobName, // Lưu blob name thay vì đường dẫn file
                FileType = extension.TrimStart('.'),
                FileSize = model.File.Length,
                CoverImageUrl = coverImageUrl, // Có thể là null hoặc blob name
                CategoryId = model.CategoryId,
                UploadedBy = model.UploadedBy,
                UploadedAt = DateTime.Now,
                IsVipOnly = model.IsVipOnly,
                ApprovalStatus = "SemiApproved", // Trạng thái mới sau khi qua validation
                ReportCount = 0,
                IsLock = false,
                ApprovalPriority = user.IsVip && user.VipExpiryDate > DateTime.Now ? 1 : 0 // VIP user có độ ưu tiên cao hơn
            };
            await _documentRepository.AddAsync(document);
            Console.WriteLine($"Document created with ID: {document.DocumentId}");

            if (model.Tags != null && model.Tags.Any())
            {
                document.DocumentTags = new HashSet<DocumentTag>(); // Khởi tạo nếu chưa
                foreach (var tagName in model.Tags.Where(tn => !string.IsNullOrWhiteSpace(tn)).Distinct())
                {
                    var tag = await _tagRepository.GetOrCreateTagAsync(tagName);
                    if (tag != null)
                    {
                        // Chỉ thêm nếu liên kết chưa tồn tại (tránh lỗi duplicate key nếu có thể)
                        if (!document.DocumentTags.Any(dt => dt.TagId == tag.TagId))
                        {
                            _context.DocumentTags.Add(new DocumentTag { DocumentId = document.DocumentId, TagId = tag.TagId });
                        }
                    }
                }
                await _context.SaveChangesAsync(); // Lưu các DocumentTag mới
            }

            var userDocument = new UserDocument
            {
                UserId = model.UploadedBy,
                DocumentId = document.DocumentId,
                ActionType = "Upload",
                AddedAt = DateTime.Now
            };
            await _userDocumentRepository.AddAsync(userDocument);


            // Thưởng cho mọi tài khoản: 1 lượt tải bonus khi upload tài liệu được duyệt
            // Cho phép người dùng chọn loại bonus download (VIP hoặc thường)
            await _userRepository.AddBonusDownloadAsync(model.UploadedBy, model.PreferVipBonus);

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
                Console.WriteLine($"Received params: Keyword={model.Keyword}, CategoryId={model.CategoryId}, FileType={model.FileType}, SchoolId={model.SchoolId}, SortBy={model.SortBy}, Page={model.Page}, PageSize={model.PageSize}, Tags={string.Join(",", model.Tags ?? new List<string>())}");

                var sortBy = model.SortBy == "UploadAt" ? "UploadedAt" : model.SortBy;

                var (documents, total) = await _documentRepository.GetPagedAsync(
                    model.Page,
                    model.PageSize,
                    string.IsNullOrEmpty(model.Keyword) ? null : model.Keyword,
                    model.CategoryId == 0 ? null : model.CategoryId,
                    string.IsNullOrEmpty(model.FileType) ? null : model.FileType,
                    sortBy,
                    model.Tags,
                    model.SchoolId == 0 ? null : model.SchoolId // Truyền SchoolId vào repository
                );

                var result = new List<object>();
                foreach (var d in documents)
                {
                    var user = await _userRepository.GetByIdAsync(d.UploadedBy);
                    result.Add(new
                    {
                        d.DocumentId,
                        d.Title,
                        Tags = d.DocumentTags.Where(dt => dt.Tag != null).Select(dt => new TagDto { TagId = dt.Tag.TagId, Name = dt.Tag.Name }).ToList(),
                        d.Description,
                        d.FileUrl,
                        d.CoverImageUrl,
                        d.FileType,
                        d.FileSize,
                        d.CategoryId,
                        d.Category,
                        d.UploadedBy,
                        Email = user?.Email ?? "Không xác định",
                        FullName = user?.FullName ?? "Ẩn danh",
                        d.UploadedAt,
                        d.DownloadCount,
                        d.IsVipOnly,
                        ApprovalStatus = d.ApprovalStatus,
                        ReportCount = d.ReportCount,
                        d.IsLock,
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

        // Cập nhật endpoint Approve
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
                
                // Tự động reset report count khi duyệt tài liệu
                document.ReportCount = 0;
                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho người đăng tài liệu
                var uploaderNotification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được duyệt.",
                    DocumentId = document.DocumentId,
                    SentAt = DateTime.Now,
                    IsRead = false
                };

                const int MaxNotificationsPerUser = 100;
                var uploaderNotificationCount = await _notificationRepository.CountByUserIdAsync(document.UploadedBy);
                if (uploaderNotificationCount >= MaxNotificationsPerUser)
                {
                    int countToDelete = uploaderNotificationCount - MaxNotificationsPerUser + 1;
                    await _notificationRepository.DeleteOldestByUserIdAsync(document.UploadedBy, countToDelete);
                }
                await _notificationRepository.AddAsync(uploaderNotification);

                // Gửi thông báo cho tất cả người theo dõi
                var uploader = await _userRepository.GetByIdAsync(document.UploadedBy);
                var followers = await _followRepository.GetFollowersByUserIdAsync(document.UploadedBy);
                foreach (var follower in followers)
                {
                    var followerNotification = new Notification
                    {
                        UserId = follower.UserId,
                        Message = $"{uploader.FullName} vừa đăng một tài liệu mới: '{document.Title}'.",
                        DocumentId = document.DocumentId,
                        SentAt = DateTime.Now,
                        IsRead = false
                    };

                    var followerNotificationCount = await _notificationRepository.CountByUserIdAsync(follower.UserId);
                    if (followerNotificationCount >= MaxNotificationsPerUser)
                    {
                        int countToDelete = followerNotificationCount - MaxNotificationsPerUser + 1;
                        await _notificationRepository.DeleteOldestByUserIdAsync(follower.UserId, countToDelete);
                    }
                    await _notificationRepository.AddAsync(followerNotification);
                }

                Console.WriteLine($"Document {id} approved and notifications sent to uploader and followers.");
                return Ok(new { Message = "Tài liệu đã được duyệt và đã gửi thông báo" });
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
                        d.IsVipOnly,
                        ApprovalStatus = d.ApprovalStatus,
                        ReportCount = d.ReportCount,
                        d.IsLock
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

                if (document.ApprovalStatus != "Approved" && document.ApprovalStatus != "SemiApproved")
                    return BadRequest(new { message = "Tài liệu chưa được duyệt." });

                if (document.IsLock)
                    return BadRequest(new { message = "Tài liệu đã bị khóa." });

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return BadRequest(new { message = "Người dùng không tồn tại." });
                // Kiểm tra quyền tải theo hệ thống VIP mới
                bool canDownload = await _userRepository.CanDownloadAsync(userId, document.IsVipOnly);
                if (!canDownload)
                {
                    if (document.IsVipOnly)
                    {
                        return BadRequest(new { message = "Bạn không thể tải tài liệu VIP này. Vui lòng nâng cấp tài khoản VIP hoặc sử dụng lượt tải VIP bonus từ việc upload tài liệu." });
                    }
                    else
                    {
                        return BadRequest(new { message = "Bạn đã hết lượt tải tài liệu thường hôm nay (2 lượt/ngày cho tài khoản thường). Vui lòng nâng cấp VIP để có 10 lượt/ngày hoặc upload tài liệu để nhận bonus download." });
                    }
                }

                // Cập nhật số lượt download đã sử dụng (bao gồm cả việc trừ bonus downloads nếu cần)
                await _userRepository.UpdateDownloadCountsAsync(userId, document.IsVipOnly);
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

                // --- BẮT ĐẦU LOGIC TỰ ĐỘNG DUYỆT ---
                const int downloadThresholdForApproval = 10; // Cấu hình ngưỡng ở đây hoặc trong appsettings.json
                
                // Tải lại thông tin document sau khi tăng DownloadCount
                var updatedDocument = await _documentRepository.GetByIdAsync(id);

                // Kiểm tra auto-approval
                if (updatedDocument.ApprovalStatus == "SemiApproved" &&
                    updatedDocument.DownloadCount >= downloadThresholdForApproval &&
                    updatedDocument.ReportCount == 0)
                {
                    updatedDocument.ApprovalStatus = "Approved";
                    await _documentRepository.UpdateAsync(updatedDocument);

                    // Gửi thông báo cho người đăng
                    var notification = new Notification
                    {
                        UserId = updatedDocument.UploadedBy,
                        Message = $"Tài liệu '{updatedDocument.Title}' của bạn đã được tự động duyệt sau khi đạt {downloadThresholdForApproval} lượt tải.",
                        DocumentId = updatedDocument.DocumentId,
                        SentAt = DateTime.Now,
                        IsRead = false
                    };
                    await _notificationRepository.AddAsync(notification);
                }

                // Kiểm tra nếu số lượng report bằng 1/10 số lượng download thì chuyển sang Pending
                if ((updatedDocument.ApprovalStatus == "SemiApproved" || updatedDocument.ApprovalStatus == "Approved") &&
                    updatedDocument.DownloadCount > 0 &&
                    updatedDocument.ReportCount >= (updatedDocument.DownloadCount / 10))
                {
                    updatedDocument.ApprovalStatus = "Pending";
                    await _documentRepository.UpdateAsync(updatedDocument);

                    // Gửi thông báo cho admin
                    var adminNotification = new Notification
                    {
                        UserId = 1, // Giả sử admin có ID = 1
                        Message = $"Tài liệu '{updatedDocument.Title}' đã bị chuyển sang trạng thái Pending do có {updatedDocument.ReportCount} báo cáo trên {updatedDocument.DownloadCount} lượt tải.",
                        DocumentId = updatedDocument.DocumentId,
                        SentAt = DateTime.Now,
                        IsRead = false
                    };
                    await _notificationRepository.AddAsync(adminNotification);

                    // Gửi thông báo cho người đăng
                    var uploaderNotification = new Notification
                    {
                        UserId = updatedDocument.UploadedBy,
                        Message = $"Tài liệu '{updatedDocument.Title}' của bạn đã bị chuyển sang trạng thái chờ xử lý do có nhiều báo cáo vi phạm.",
                        DocumentId = updatedDocument.DocumentId,
                        SentAt = DateTime.Now,
                        IsRead = false
                    };
                    await _notificationRepository.AddAsync(uploaderNotification);
                }
                // --- KẾT THÚC LOGIC TỰ ĐỘNG DUYỆT ---

                // Tạo SAS URL cho download (có thể dùng cách này để redirect)
                // Loại bỏ prefix "documents/" nếu có để tránh duplicate path
                var blobPath = document.FileUrl.StartsWith("documents/")
                    ? document.FileUrl.Substring("documents/".Length)
                    : document.FileUrl;
                var sasUrl = _blob.GetReadSasUrl("documents", blobPath, TimeSpan.FromMinutes(10));
                return Ok(new { url = sasUrl, fileName = $"{document.Title}.{document.FileType}" });

                // Hoặc có thể stream file qua API (không khuyến khích cho file lớn)
                // var stream = await _blob.DownloadAsync("documents", document.FileUrl);
                // return File(stream, $"application/{document.FileType}", $"{document.Title}.{document.FileType}");
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

            if (document.ApprovalStatus != "Approved" && document.ApprovalStatus != "SemiApproved")
            {
                Console.WriteLine($"Document {id} is not approved. Current status: {document.ApprovalStatus}");
                return BadRequest("Tài liệu chưa được duyệt.");
            }

            if (document.IsLock)
            {
                Console.WriteLine($"Document {id} is locked.");
                return BadRequest("Tài liệu đã bị khóa.");
            }

            if (document.FileType.ToLower() != "pdf")
            {
                Console.WriteLine($"Document {id} is not a PDF.");
                return Ok(new { Message = "Chỉ hỗ trợ xem trước file PDF." });
            }

            try
            {
                // Tạo SAS URL cho preview PDF
                // Loại bỏ prefix "documents/" nếu có để tránh duplicate path
                var blobPath = document.FileUrl.StartsWith("documents/")
                    ? document.FileUrl.Substring("documents/".Length)
                    : document.FileUrl;
                var sasUrl = _blob.GetReadSasUrl("documents", blobPath, TimeSpan.FromMinutes(5));
                return Ok(new { url = sasUrl });

                // Hoặc có thể stream file qua API
                // var stream = await _blob.DownloadAsync("documents", document.FileUrl);
                // return File(stream, "application/pdf", enableRangeProcessing: true);
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
                Console.WriteLine($"!!!! ERROR in GetTopDownloadedDocument: {ex.ToString()} !!!!");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("rankings/top-downloads")]
        public async Task<IActionResult> GetRankingsByTopDownloads([FromQuery] int limit = 5)
        {
            try
            {
                var topDocuments = await _context.Documents
                    .Include(d => d.User)
                    .Where(d => (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "SemiApproved") && !d.IsLock)
                    .OrderByDescending(d => d.DownloadCount)
                    .Take(limit)
                    .Select(d => new
                    {
                        d.DocumentId,
                        d.Title,
                        d.CoverImageUrl,
                        d.DownloadCount,
                        UploadedByUser = d.User != null ? new { d.User.FullName } : null
                    })
                    .ToListAsync();

                if (topDocuments == null || !topDocuments.Any())
                {
                    return Ok(new List<object>());
                }

                return Ok(topDocuments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRankingsByTopDownloads: {ex.ToString()}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalDocuments = await _context.Documents.CountAsync();
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

        [HttpPut("{id}/lock")]
        public async Task<IActionResult> LockUnlockDocument(int id, [FromBody] LockDocumentModel model)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound("Tài liệu không tồn tại.");

                await _documentRepository.UpdateLockStatusAsync(id, model.IsLocked);

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
                            (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "SemiApproved") &&
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



        [HttpPut("{id}/reset-reports")]
        public async Task<IActionResult> ResetReportCount(int id)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound("Tài liệu không tồn tại.");

                // Reset report count và khôi phục trạng thái
                document.ReportCount = 0;
                if (document.ApprovalStatus == "Pending")
                {
                    document.ApprovalStatus = "SemiApproved"; // Khôi phục về trạng thái bán duyệt
                }

                await _documentRepository.UpdateAsync(document);

                // Gửi thông báo cho người đăng
                var notification = new Notification
                {
                    UserId = document.UploadedBy,
                    Message = $"Tài liệu '{document.Title}' của bạn đã được khôi phục trạng thái sau khi xem xét báo cáo.",
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

                return Ok(new { Message = "Đã reset số lượng báo cáo và khôi phục trạng thái tài liệu." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting report count for document {id}: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        [HttpGet("related-by-tags")]
        public async Task<IActionResult> GetRelatedDocumentsByTags([FromQuery] List<string> tagNames, [FromQuery] int excludeDocumentId, [FromQuery] int limit = 5)
        {
            if (tagNames == null || !tagNames.Any())
            {
                return Ok(new List<object>());
            }

            var documents = await _documentRepository.GetRelatedDocumentsByTagsAsync(tagNames, excludeDocumentId, limit);

            var result = documents.Select(d => new
            {
                d.DocumentId,
                d.Title,
                d.CoverImageUrl,
                UploaderFullName = d.User?.FullName,
            }).ToList();

            return Ok(result);
        }

        private string GetMimeTypeByExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
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
        public bool IsVipOnly { get; set; }
    }

    public class UploadDocumentModel
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string Title { get; set; }
        public string Description { get; set; }
        [Required(ErrorMessage = "Danh mục không được để trống")]
        public int CategoryId { get; set; }
        [Required(ErrorMessage = "Người tải lên không được để trống")]
        public int UploadedBy { get; set; }
        public bool IsVipOnly { get; set; } = false; // Tài liệu VIP hay thường
        public bool PreferVipBonus { get; set; } = false; // Người dùng muốn nhận VIP bonus download thay vì regular bonus
        public IFormFile File { get; set; }
        public IFormFile? CoverImage { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class SearchDocumentModel
    {
        public string Keyword { get; set; } = "";
        public int CategoryId { get; set; } = 0;
        public string FileType { get; set; } = "";
        public string SortBy { get; set; } = "UploadedAt";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public List<string>? Tags { get; set; }
        public int SchoolId { get; set; } = 0;
    }

    public class LockDocumentModel
    {
        public bool IsLocked { get; set; }
    }

    public class TagDto
    {
        public int TagId { get; set; }
        public string Name { get; set; }
    }


}
