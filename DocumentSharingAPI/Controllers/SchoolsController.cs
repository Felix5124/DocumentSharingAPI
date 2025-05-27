using DocumentSharingAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SchoolsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SchoolsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/schools
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var schools = await _context.Schools
                .Select(s => new
                {
                    s.SchoolId,
                    s.Name,
                    s.LogoUrl,
                    s.ExternalUrl,
                    UserCount = _context.Users.Count(u => u.SchoolId == s.SchoolId),
                    DocumentCount = _context.Documents.Count(d => d.SchoolId == s.SchoolId)
                })
                .ToListAsync();
            return Ok(schools);
        }

        // POST: api/schools
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] UploadSchoolModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Name))
            {
                return BadRequest("Tên trường không được để trống.");
            }

            if (model.Logo == null || model.Logo.Length == 0)
            {
                return BadRequest("Ảnh logo không được để trống. Vui lòng upload ảnh logo.");
            }

            if (string.IsNullOrEmpty(model.ExternalUrl))
            {
                return BadRequest("URL bên ngoài không được để trống.");
            }

            // Kiểm tra định dạng URL
            if (!Uri.TryCreate(model.ExternalUrl, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("URL không hợp lệ. Vui lòng cung cấp URL bắt đầu bằng http hoặc https.");
            }

            var existingSchool = await _context.Schools.FirstOrDefaultAsync(s => s.Name == model.Name);
            if (existingSchool != null)
            {
                return BadRequest("Trường học đã tồn tại.");
            }

            // Xử lý logo
            var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".heic", ".heif" };
            var extension = Path.GetExtension(model.Logo.FileName).ToLowerInvariant();

            if (!allowedImageExtensions.Contains(extension))
            {
                return BadRequest("Định dạng logo không hợp lệ. Chỉ chấp nhận JPG, PNG, GIF, TIFF, TIF, HEIC, HEIF.");
            }

            var logosDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos");
            if (!Directory.Exists(logosDirectory))
            {
                Directory.CreateDirectory(logosDirectory);
            }

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Logo.FileName)}";
            var filePath = Path.Combine(logosDirectory, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.Logo.CopyToAsync(stream);
            }

            var school = new School
            {
                Name = model.Name,
                LogoUrl = $"logos/{fileName}",
                ExternalUrl = model.ExternalUrl
            };

            _context.Schools.Add(school);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = school.SchoolId }, new
            {
                school.SchoolId,
                school.Name,
                school.LogoUrl,
                school.ExternalUrl
            });
        }

        // PUT: api/schools/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] UploadSchoolModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Name))
            {
                return BadRequest("Tên trường không được để trống.");
            }

            if (string.IsNullOrEmpty(model.ExternalUrl))
            {
                return BadRequest("URL bên ngoài không được để trống.");
            }

            // Kiểm tra định dạng URL
            if (!Uri.TryCreate(model.ExternalUrl, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("URL không hợp lệ. Vui lòng cung cấp URL bắt đầu bằng http hoặc https.");
            }

            var school = await _context.Schools.FindAsync(id);
            if (school == null)
            {
                return NotFound("Trường học không tồn tại.");
            }

            // Kiểm tra tên trường trùng lặp (ngoại trừ trường hiện tại)
            var existingSchool = await _context.Schools
                .FirstOrDefaultAsync(s => s.Name == model.Name && s.SchoolId != id);
            if (existingSchool != null)
            {
                return BadRequest("Tên trường học đã tồn tại.");
            }

            // Xử lý logo nếu có
            string newLogoUrl = school.LogoUrl;
            if (model.Logo != null && model.Logo.Length > 0)
            {
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".heic", ".heif" };
                var extension = Path.GetExtension(model.Logo.FileName).ToLowerInvariant();

                if (!allowedImageExtensions.Contains(extension))
                {
                    return BadRequest("Định dạng logo không hợp lệ. Chỉ chấp nhận JPG, PNG, GIF, TIFF, TIF, HEIC, HEIF.");
                }

                // Xóa logo cũ nếu không phải logo mặc định
                if (!string.IsNullOrEmpty(school.LogoUrl) && !school.LogoUrl.Contains("default.jpg"))
                {
                    var oldLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", school.LogoUrl);
                    if (System.IO.File.Exists(oldLogoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldLogoPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old logo file {oldLogoPath}: {ex.Message}");
                        }
                    }
                }

                // Lưu logo mới
                var logosDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos");
                if (!Directory.Exists(logosDirectory))
                {
                    Directory.CreateDirectory(logosDirectory);
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Logo.FileName)}";
                var filePath = Path.Combine(logosDirectory, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Logo.CopyToAsync(stream);
                }

                newLogoUrl = $"logos/{fileName}";
            }

            // Cập nhật thông tin trường
            school.Name = model.Name;
            school.LogoUrl = newLogoUrl;
            school.ExternalUrl = model.ExternalUrl;

            _context.Schools.Update(school);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                school.SchoolId,
                school.Name,
                school.LogoUrl,
                school.ExternalUrl
            });
        }

        // DELETE: api/schools/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var school = await _context.Schools.FindAsync(id);
            if (school == null)
                return NotFound("Trường học không tồn tại.");

            // Kiểm tra xem trường có đang được liên kết với user hoặc document không
            var hasUsers = await _context.Users.AnyAsync(u => u.SchoolId == id);
            var hasDocuments = await _context.Documents.AnyAsync(d => d.SchoolId == id);
            if (hasUsers || hasDocuments)
                return BadRequest("Không thể xóa trường học vì đang có user hoặc tài liệu liên kết.");

            // Xóa logo nếu không phải logo mặc định
            if (!string.IsNullOrEmpty(school.LogoUrl) && !school.LogoUrl.Contains("default.jpg"))
            {
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", school.LogoUrl);
                if (System.IO.File.Exists(logoPath))
                {
                    try
                    {
                        System.IO.File.Delete(logoPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting logo file {logoPath}: {ex.Message}");
                    }
                }
            }

            _context.Schools.Remove(school);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
    public class UploadSchoolModel
    {
        [Required(ErrorMessage = "Tên trường không được để trống")]
        public string Name { get; set; }

        public IFormFile? Logo { get; set; } // Không bắt buộc khi sửa

        [Required(ErrorMessage = "URL bên ngoài không được để trống")]
        [Url(ErrorMessage = "URL không hợp lệ")]
        public string ExternalUrl { get; set; }
    }
}