using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Google;
using System.Text.Json;
using DocumentSharingAPI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using DocumentSharingAPI.Helpers;

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
        private readonly IImageService _imageService;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(IDocumentRepository documentRepository, ICategoryRepository categoryRepository, IUserRepository userRepository, AppDbContext context, IUserDocumentRepository userDocumentRepository, IImageService imageService, IWebHostEnvironment env)
        {
            _documentRepository = documentRepository;
            _categoryRepository = categoryRepository;
            _userRepository = userRepository;
            _context = context;
            _userDocumentRepository = userDocumentRepository;
            _imageService = imageService;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var query = _context.Documents
                    .Include(d => d.User) // Để lấy Email
                    .Include(d => d.Category) // Để lấy CategoryName
                    .Where(d => d.IsApproved) // Chỉ lấy tài liệu đã duyệt
                    .OrderByDescending(d => d.UploadedAt);

            var totalItems = await query.CountAsync();
            var documents = await query
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();


            var result = documents.Select(d => new
            {
                d.DocumentId,
                d.Title,
                d.Description,
                d.FileUrl,
                d.FileType,
                d.FileSize,
                d.CoverImageUrl,
                d.UploadedAt,
                d.DownloadCount,
                d.PointsRequired,
                d.IsApproved,
                CategoryName = d.Category?.Name,
                UploadedByEmail = d.User?.Email ?? "Không xác định"
            }).ToList();

            return Ok(new { TotalItems = totalItems, Page = page, PageSize = pageSize, Data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var document = await _context.Documents
                                         .Include(d => d.User)
                                         .Include(d => d.Category)
                                         .FirstOrDefaultAsync(d => d.DocumentId == id);
            if (document == null)
                return NotFound();

            // Chỉ trả về nếu đã được duyệt, hoặc nếu người dùng là admin/người upload
            if (!document.IsApproved )
                return Forbid("Tài liệu chưa được duyệt");

            return Ok(new
            {
                document.DocumentId,
                document.Title,
                document.Description,
                document.FileUrl,
                document.FileType,
                document.FileSize,
                document.CoverImageUrl,
                document.UploadedAt,
                document.DownloadCount,
                document.PointsRequired,
                document.IsApproved,
                CategoryId = document.CategoryId,
                CategoryName = document.Category?.Name,
                UploadedById = document.UploadedBy,
                UploadedByEmail = document.User?.Email
            });


        }

        // Endpoint này có thể dùng cho việc tạo Document với FileUrl đã có sẵn (ví dụ file từ Google Drive)
        // Hoặc tạo metadata trước, rồi upload file sau (cần luồng phức tạp hơn)
        // Thông thường, việc upload file và tạo metadata sẽ đi chung trong một request [FromForm]
        [HttpPost("metadata")] 
        [Authorize]
        public async Task<IActionResult> CreateMetadata([FromBody] DocumentModel model)
        {
            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized();

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null) return BadRequest("Document title already exists.");

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null) return BadRequest("Invalid category.");

            var document = new Document
            {
                Title = model.Title, Description = model.Description, FileUrl = model.FileUrl,
                FileType = model.FileType, FileSize = model.FileSize, CategoryId = model.CategoryId,
                UploadedBy = currentUserId.Value, // Lấy từ user đã xác thực
                UploadedAt = DateTime.Now, PointsRequired = model.PointsRequired, IsApproved = false
            };
            await _documentRepository.AddAsync(document);
            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }


        [HttpPost]
        
        [Authorize]
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
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized();

            bool isAdmin = await this.IsCurrentUserAdminAsync(_userRepository);
            if (document.UploadedBy != currentUserId.Value && !isAdmin)
            {
                return Forbid("Bạn không có quyền chỉnh sửa tài liệu này.");
            }


            if (model.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(model.CategoryId.Value);
                if (category == null) return BadRequest("Danh mục không hợp lệ.");
                document.CategoryId = model.CategoryId.Value;
            }


            document.Title = model.Title ?? document.Title;
            document.Description = model.Description ?? document.Description;
            if (model.PointsRequired.HasValue)
            {
                document.PointsRequired = model.PointsRequired.Value;
            }
            // Xử lý cập nhật ảnh bìa
            if (model.NewCoverImageFile != null && model.NewCoverImageFile.Length > 0)
            {
                // SaveFileAsync sẽ xóa document.CoverImageUrl (nếu có) trước khi lưu file mới
                document.CoverImageUrl = await _imageService.SaveFileAsync(model.NewCoverImageFile, "ImageDocuments", document.CoverImageUrl);
            }
            else if (model.RemoveCoverImage && !string.IsNullOrEmpty(document.CoverImageUrl))
            {
                await _imageService.DeleteFileAsync(document.CoverImageUrl);
                document.CoverImageUrl = null;
            }

            await _documentRepository.UpdateAsync(document);
            return Ok(document);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized();

            bool isAdmin = await this.IsCurrentUserAdminAsync(_userRepository);
            if (document.UploadedBy != currentUserId.Value && !isAdmin)
            {
                return Forbid("Bạn không có quyền xóa tài liệu này.");
            }



            // Xóa ảnh bìa
            if (!string.IsNullOrEmpty(document.FileUrl))
            {
                await _imageService.DeleteFileAsync(document.FileUrl);
            }
            if (!string.IsNullOrEmpty(document.CoverImageUrl))
            {
                await _imageService.DeleteFileAsync(document.CoverImageUrl);
            }


            await _documentRepository.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentModel model)
        {

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized("Không thể xác định người dùng. Vui lòng đăng nhập lại.");

            var user = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (user == null) return BadRequest("Người dùng không tồn tại."); // Should not happen if token is valid

            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null) return BadRequest("Danh mục không hợp lệ.");

            var existingDocument = await _documentRepository.GetByTitleAsync(model.Title);
            if (existingDocument != null) return BadRequest("Tiêu đề tài liệu đã tồn tại.");

            if (model.File == null || model.File.Length == 0) return BadRequest("Không có file nào được tải lên.");

            //Lưu file
            var documentFileUrl = await _imageService.SaveFileAsync(model.File, "Documents");
            if (string.IsNullOrEmpty(documentFileUrl))
            {
                return BadRequest("Không thể lưu file tài liệu.");
            }


            //Xử lý ảnh bìa
            string coverImageUrl = null;
            if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
            {
                coverImageUrl = await _imageService.SaveFileAsync(model.CoverImageFile, "ImageDocuments");
            }
            else if (model.File.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
                                     model.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // documentFileUrl là đường dẫn tương đối ("Files/Documents/abc.pdf")
                // ExtractFirstPageAsImageAsync (overload thứ 2) nhận đường dẫn này
                coverImageUrl = await _imageService.ExtractFirstPageAsImageAsync(documentFileUrl, "Covers/PdfPreviews", model.Title);
            }


            var document = new Document
            {
                Title = model.Title,
                Description = model.Description,
                FileUrl = documentFileUrl,
                FileType = Path.GetExtension(model.File.FileName).ToLower().Replace(".", ""),
                FileSize = model.File.Length,
                CoverImageUrl = coverImageUrl,
                CategoryId = model.CategoryId,
                UploadedBy = currentUserId.Value,
                UploadedAt = DateTime.Now,
                PointsRequired = model.PointsRequired,
                IsApproved = false // Mặc định tài liệu mới cần duyệt

            };
            await _documentRepository.AddAsync(document);

            // Thêm vào UserDocument với ActionType = Upload
            var userDocument = new UserDocument
            {
                UserId = currentUserId.Value,
                DocumentId = document.DocumentId,
                ActionType = "Upload",
                AddedAt = DateTime.Now
            };

            await _userDocumentRepository.AddAsync(userDocument);

            // Cộng điểm
            await _userRepository.UpdatePointsAsync(currentUserId.Value, 10); // Cộng điểm

            // Gửi thông báo cho người theo dõi
            var follows = await _context.Follows
                .Where(f => f.FollowedUserId == currentUserId.Value || f.CategoryId == model.CategoryId)
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
            var uploadCount = await _context.Documents.CountAsync(d => d.UploadedBy == currentUserId.Value);
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
                    await _context.SaveChangesAsync(); // Lưu Badge trước khi sử dụng BadgeId
                }

                var userBadge = await _context.UserBadges
                    .FirstOrDefaultAsync(ub => ub.UserId == currentUserId.Value && ub.BadgeId == badge.BadgeId);
                if (userBadge == null)
                {
                    userBadge = new UserBadge
                    {
                        UserId = currentUserId.Value,
                        BadgeId = badge.BadgeId,
                        EarnedAt = DateTime.Now
                    };
                    await _context.UserBadges.AddAsync(userBadge);
                }
            }

            await _context.SaveChangesAsync(); // Lưu tất cả thay đổi cuối cùng

            return CreatedAtAction(nameof(GetById), new { id = document.DocumentId }, document);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] SearchDocumentModel model)
        {

            // GetPagedAsync trong repository cần Include User và Category

            var (documents, total) = await _documentRepository.GetPagedAsync(
                model.Page ?? 1, model.PageSize ?? 10, model.Keyword,
                model.CategoryId, model.FileType, model.SortBy);

            var result = documents.Select(d => new
            {
                d.DocumentId,
                d.Title,
                d.Description,
                d.FileType,
                d.FileSize,
                d.CoverImageUrl,
                d.UploadedAt,
                d.DownloadCount,
                d.PointsRequired,
                d.IsApproved,
                CategoryName = d.Category?.Name,
                UploadedByEmail = d.User?.Email
            }).ToList();
            return Ok(new { Documents = result, Total = total, Page = model.Page, PageSize = model.PageSize });
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPending()
        {
            var documents = await _documentRepository.GetPendingDocumentsAsync();
            var result = documents.Select(d => new // Bổ sung thông tin User, Category
            {
                d.DocumentId,
                d.Title,
                d.FileUrl,
                d.UploadedAt,
                CategoryName = d.Category?.Name, // Cần Include trong GetPendingDocumentsAsync
                UploadedByEmail = d.User?.Email   // Cần Include trong GetPendingDocumentsAsync
            }).ToList();
            return Ok(result);

        }

        [HttpPut("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null) return NotFound();

            if (document.IsApproved) return BadRequest("Tài liệu đã được duyệt trước đó.");

            await _documentRepository.ApproveDocumentAsync(id);
            return Ok(new { Message = "Tài liệu đã được duyệt thành công." });
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id)
        {
            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized("Vui lòng đăng nhập để tải tài liệu.");

            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null) return NotFound("Tài liệu không tồn tại.");

            if (!document.IsApproved) return BadRequest("Tài liệu này chưa được duyệt.");

            var user = await _userRepository.GetByIdAsync(currentUserId.Value); // Lấy thông tin user hiện tại
            if (user == null) return Unauthorized("Người dùng không hợp lệ.");


            // Người upload không bị trừ điểm, người khác thì bị trừ điểm 
            if (document.UploadedBy != currentUserId.Value)
            {
                if (user.Points < document.PointsRequired)
                {
                    return BadRequest($"Không đủ điểm. Bạn cần {document.PointsRequired} điểm, hiện có {user.Points} điểm.");
                }
                if (document.PointsRequired > 0)
                {
                    await _userRepository.UpdatePointsAsync(currentUserId.Value, -document.PointsRequired);
                }
            }


            await _documentRepository.IncrementDownloadCountAsync(id);

            var userDocument = await _userDocumentRepository.GetByUserIdDocumentIdAndActionAsync(currentUserId.Value, id, "Download");
            if (userDocument == null)
            {
                userDocument = new UserDocument
                {
                    UserId = currentUserId.Value,
                    DocumentId = id,
                    ActionType = "Download",
                    AddedAt = DateTime.Now
                };
                await _userDocumentRepository.AddAsync(userDocument);
            }

            var recommendation = new Recommendation
            {
                UserId = currentUserId.Value,
                DocumentId = id,
                InteractedAt = DateTime.Now,
            };
            await _context.Recommendations.AddAsync(recommendation);
            await _context.SaveChangesAsync();


            var physicalFilePath = Path.Combine(_env.ContentRootPath, document.FileUrl.TrimStart('/'));
            if (!System.IO.File.Exists(physicalFilePath))
            {
                return NotFound("File không tồn tại trên server. Vui lòng liên hệ quản trị viên.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalFilePath);
            var mimeType = GetMimeType(document.FileType);
            return File(fileBytes, mimeType, $"{document.Title}.{document.FileType}");

        }

        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(int id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            if (!document.IsApproved)
                return BadRequest("Tài liệu này chưa được duyệt.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), document.FileUrl);
            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            // Logic preview: ưu tiên file PDF, sau đó là ảnh bìa
            if (document.FileType.ToLower() == "pdf" && !string.IsNullOrEmpty(document.FileUrl))
            {
                var pdfPath = Path.Combine(_env.ContentRootPath, document.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(pdfPath))
                {
                    try
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
                        return File(fileBytes, "application/pdf", enableRangeProcessing: true);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest($"Lỗi khi tạo preview PDF: {ex.Message}");
                    }
                }
            }

            // Nếu không phải PDF hoặc file PDF không có, thử trả về ảnh bìa
            if (!string.IsNullOrEmpty(document.CoverImageUrl))
            {
                var coverPath = Path.Combine(_env.ContentRootPath, document.CoverImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(coverPath))
                {
                    try
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(coverPath);
                        return File(fileBytes, GetMimeType(Path.GetExtension(document.CoverImageUrl).TrimStart('.')), enableRangeProcessing: true);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest($"Lỗi khi tạo preview ảnh bìa: {ex.Message}");
                    }
                }
            }
            return BadRequest("Không có nội dung phù hợp để preview cho tài liệu này.");
            
        }

        private string GetMimeType(string fileExtension)
        {
            fileExtension = fileExtension?.ToLower().TrimStart('.') ?? "";
            switch (fileExtension)
            {
                case "pdf": return "application/pdf";
                case "doc": return "application/msword";
                case "docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case "xls": return "application/vnd.ms-excel";
                case "xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case "ppt": return "application/vnd.ms-powerpoint";
                case "pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case "txt": return "text/plain";
                case "jpg": case "jpeg": return "image/jpeg";
                case "png": return "image/png";
                case "gif": return "image/gif";
                default: return "application/octet-stream";
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
        [System.ComponentModel.DataAnnotations.Required]
        public string Title { get; set; }
        public string Description { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        public int CategoryId { get; set; }
        public int PointsRequired { get; set; } = 0;
        [System.ComponentModel.DataAnnotations.Required]
        public IFormFile File { get; set; } // File tài liệu chính
        public IFormFile? CoverImageFile { get; set; } // File ảnh bìa (tùy chọn)
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

    public class UpdateDocumentModel 
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int? CategoryId { get; set; } // Nullable để cho phép không cập nhật
        public int? PointsRequired { get; set; } // Nullable
        public IFormFile? NewCoverImageFile { get; set; }
        public bool RemoveCoverImage { get; set; } = false;


    }


}