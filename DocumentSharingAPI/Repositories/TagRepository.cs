using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public class TagRepository : ITagRepository
    {
        private readonly AppDbContext _context;

        public TagRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Tag> GetByIdAsync(int id)
        {
            return await _context.Tags.FindAsync(id);
        }

        public async Task<Tag> GetByNameAsync(string name)
        {
            // Luôn chuẩn hóa tên khi tìm kiếm để đảm bảo tính nhất quán
            var normalizedName = name.Trim().ToLowerInvariant();
            return await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLowerInvariant() == normalizedName);
        }

        public async Task<IEnumerable<Tag>> GetAllAsync()
        {
            return await _context.Tags.OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<Tag> AddAsync(Tag tag)
        {
            // Đảm bảo tên được trim, cân nhắc việc lưu tên ở dạng chuẩn hóa nếu muốn
            tag.Name = tag.Name.Trim();
            tag.CreatedAt = DateTime.UtcNow;
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return tag;
        }

        public async Task<Tag> GetOrCreateTagAsync(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            var normalizedTagName = tagName.Trim().ToLowerInvariant();
            // Sử dụng tên đã chuẩn hóa để tìm kiếm
            var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedTagName.ToLower());
            if (existingTag != null)
            {
                return existingTag;
            }

            // Nếu chưa tồn tại, tạo tag mới với tên gốc đã được trim
            var newTag = new Tag { Name = tagName.Trim(), CreatedAt = DateTime.UtcNow };
            _context.Tags.Add(newTag);
            await _context.SaveChangesAsync();
            return newTag;
        }

        public async Task UpdateAsync(Tag tag)
        {
            tag.Name = tag.Name.Trim();
            tag.UpdatedAt = DateTime.UtcNow;
            _context.Tags.Update(tag);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag != null)
            {
                _context.Tags.Remove(tag);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Tag>> GetTagsByDocumentIdAsync(int documentId)
        {
            return await _context.DocumentTags
                .Where(dt => dt.DocumentId == documentId)
                .Select(dt => dt.Tag)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }
    }
}