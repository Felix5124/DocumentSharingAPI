using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly ITagRepository _tagRepository;
        private readonly AppDbContext _context; // Để kiểm tra DocumentTags hoặc Document

        public TagsController(ITagRepository tagRepository, AppDbContext context)
        {
            _tagRepository = tagRepository;
            _context = context;
        }

        // GET: api/tags
        [HttpGet]
        public async Task<IActionResult> GetAllTags()
        {
            var tags = await _tagRepository.GetAllAsync();
            return Ok(tags);
        }

        // GET: api/tags/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTagById(int id)
        {
            var tag = await _tagRepository.GetByIdAsync(id);
            if (tag == null)
            {
                return NotFound(new { message = $"Không tìm thấy tag với ID {id}." });
            }
            return Ok(tag);
        }

        // POST: api/tags
        // [Authorize(Roles = "Admin")] // Admin mới được tạo tag trực tiếp
        [HttpPost]
        public async Task<IActionResult> CreateTag([FromBody] TagCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingTag = await _tagRepository.GetByNameAsync(model.Name); // GetByNameAsync nên tìm theo tên đã chuẩn hóa
            if (existingTag != null)
            {
                return Conflict(new { message = $"Tag '{model.Name.Trim()}' đã tồn tại.", existingTag });
            }

            var newTag = new Tag { Name = model.Name.Trim() };
            var createdTag = await _tagRepository.AddAsync(newTag);
            return CreatedAtAction(nameof(GetTagById), new { id = createdTag.TagId }, createdTag);
        }

        // PUT: api/tags/{id}
        // [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTag(int id, [FromBody] TagUpdateModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tagToUpdate = await _tagRepository.GetByIdAsync(id);
            if (tagToUpdate == null)
            {
                return NotFound(new { message = $"Không tìm thấy tag với ID {id}." });
            }

            // Kiểm tra tên mới có trùng với tag khác không (ngoại trừ chính nó)
            var newTagNameNormalized = model.Name.Trim().ToLowerInvariant();
            var existingTagWithNewName = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagId != id && t.Name.ToLowerInvariant() == newTagNameNormalized);

            if (existingTagWithNewName != null)
            {
                return Conflict(new { message = $"Tên tag '{model.Name.Trim()}' đã được sử dụng bởi một tag khác." });
            }

            tagToUpdate.Name = model.Name.Trim();
            await _tagRepository.UpdateAsync(tagToUpdate);
            return Ok(tagToUpdate);
        }

        // DELETE: api/tags/{id}
        // [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tagToDelete = await _tagRepository.GetByIdAsync(id);
            if (tagToDelete == null)
            {
                return NotFound(new { message = $"Không tìm thấy tag với ID {id}." });
            }

            // Kiểm tra xem tag có đang được sử dụng không
            bool isTagInUse = await _context.DocumentTags.AnyAsync(dt => dt.TagId == id);
            if (isTagInUse)
            {
                // Nếu bạn đặt OnDelete Cascade cho DocumentTag -> Tag, thì không cần kiểm tra này
                // Nhưng nếu bạn muốn thông báo rõ ràng cho admin thì nên kiểm tra
                return BadRequest(new { message = $"Không thể xóa tag '{tagToDelete.Name}' vì đang được gán cho một hoặc nhiều tài liệu." });
            }

            await _tagRepository.DeleteAsync(id); // Repository sẽ xử lý việc xóa
            return Ok(new { message = $"Tag '{tagToDelete.Name}' đã được xóa." });
        }

        // GET: api/tags/document/{documentId}
        [HttpGet("document/{documentId:int}")]
        public async Task<IActionResult> GetTagsByDocumentId(int documentId)
        {
            var documentExists = await _context.Documents.AnyAsync(d => d.DocumentId == documentId);
            if (!documentExists)
            {
                return NotFound(new { message = $"Không tìm thấy tài liệu với ID {documentId}." });
            }
            var tags = await _tagRepository.GetTagsByDocumentIdAsync(documentId);
            return Ok(tags);
        }

        // GET: api/tags/search?name=abc (Dùng cho autocomplete)
        [HttpGet("search")]
        public async Task<IActionResult> SearchTags([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                // Có thể trả về top N tags phổ biến hoặc danh sách rỗng tùy logic
                return Ok(new List<Tag>());
            }
            var normalizedName = name.Trim().ToLowerInvariant();
            var tags = await _context.Tags
                                     .Where(t => t.Name.ToLowerInvariant().Contains(normalizedName))
                                     .OrderBy(t => t.Name)
                                     .Take(10) // Giới hạn số lượng kết quả
                                     .ToListAsync();
            return Ok(tags);
        }
    }

    // DTOs for TagController
    public class TagCreateModel
    {
        [Required(ErrorMessage = "Tên tag không được để trống")]
        [MaxLength(100, ErrorMessage = "Tên tag không được vượt quá 100 ký tự")]
        public string Name { get; set; }
    }

    public class TagUpdateModel
    {
        [Required(ErrorMessage = "Tên tag không được để trống")]
        [MaxLength(100, ErrorMessage = "Tên tag không được vượt quá 100 ký tự")]
        public string Name { get; set; }
    }
}