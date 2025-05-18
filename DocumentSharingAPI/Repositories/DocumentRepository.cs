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

        public new async Task<Document> GetByIdAsync(int id)
        {
            return await _context.Documents.FindAsync(id);
        }

        public async Task IncrementDownloadCountAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {id} not found.");
                throw new Exception("Document not found");
            }

            Console.WriteLine($"Found document ID {id}, current DownloadCount: {document.DownloadCount}");
            document.DownloadCount++;
            Console.WriteLine($"Incrementing DownloadCount to: {document.DownloadCount}");
            await _context.SaveChangesAsync();
            Console.WriteLine($"DownloadCount updated for document ID {id} to {document.DownloadCount}");
        }

        public async Task<(IEnumerable<Document>, int)> GetPagedAsync(int page, int pageSize, string keyword, int? categoryId, string fileType, string sortBy)
        {
            var query = _context.Documents.AsQueryable();

            // Lọc theo isApproved = true ngay từ đầu
            query = query.Where(d => d.IsApproved == true);

            // Lọc theo keyword
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            // Lọc theo categoryId
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            // Lọc theo fileType
            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            // Sắp xếp
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

            // Tính tổng số tài liệu đã được phê duyệt
            var total = await query.CountAsync();

            // Phân trang
            var documents = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (documents, total);
        }

        public async Task<int> GetTotalCountAsync(string keyword, int? categoryId, string fileType, bool isApproved = false)
        {
            var query = _context.Documents.AsQueryable();

            // Lọc theo keyword
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            // Lọc theo categoryId
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            // Lọc theo fileType
            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            // Lọc theo isApproved
            if (isApproved)
            {
                query = query.Where(d => d.IsApproved == true);
            }

            return await query.CountAsync();
        }
    }
}