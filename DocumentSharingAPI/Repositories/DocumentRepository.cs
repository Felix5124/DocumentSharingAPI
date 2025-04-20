using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class DocumentRepository : Repository<Document>, IDocumentRepository
    {
        public DocumentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Document> GetByTitleAsync(string title)
        {
            return await _context.Documents.FirstOrDefaultAsync(d => d.Title == title);
        }

        public async Task<IEnumerable<Document>> SearchAsync(string keyword, int? categoryId, string fileType, string sortBy)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            if (categoryId.HasValue)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            switch (sortBy?.ToLower())
            {
                case "newest":
                    query = query.OrderByDescending(d => d.UploadedAt);
                    break;
                case "popular":
                    query = query.OrderByDescending(d => d.DownloadCount);
                    break;
                default:
                    query = query.OrderBy(d => d.Title);
                    break;
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetPendingDocumentsAsync()
        {
            return await _context.Documents.Where(d => !d.IsApproved).ToListAsync();
        }

        public async Task ApproveDocumentAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
                throw new Exception("Document not found");

            document.IsApproved = true;
            await _context.SaveChangesAsync();
        }

        public async Task IncrementDownloadCountAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
                throw new Exception("Document not found");

            document.DownloadCount++;
            await _context.SaveChangesAsync();
        }

        public async Task<(IEnumerable<Document>, int)> GetPagedAsync(int page, int pageSize, string keyword, int? categoryId, string fileType, string sortBy)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            if (categoryId.HasValue)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            switch (sortBy?.ToLower())
            {
                case "newest":
                    query = query.OrderByDescending(d => d.UploadedAt);
                    break;
                case "popular":
                    query = query.OrderByDescending(d => d.DownloadCount);
                    break;
                default:
                    query = query.OrderBy(d => d.Title);
                    break;
            }

            var total = await query.CountAsync();
            var documents = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (documents, total);
        }
    }
}