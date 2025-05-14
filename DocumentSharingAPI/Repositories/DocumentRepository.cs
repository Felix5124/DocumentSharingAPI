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
            return await _context.Documents
                .Include(d => d.User)
                .Include(d => d.Category) 
                .FirstOrDefaultAsync(d => d.Title == title);
        }

        public async Task<IEnumerable<Document>> SearchAsync(string keyword, int? categoryId, string fileType, string sortBy)
        {
            var query = _context.Documents
                                            .Include(d => d.User)
                                            .Include(d => d.Category)
                                            .AsQueryable();
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
            return await _context.Documents
                                 .Where(d => !d.IsApproved)
                                 .Include(d => d.User) 
                                 .Include(d => d.Category) 
                                 .OrderByDescending(d => d.UploadedAt) 
                                 .ToListAsync();
        }

        public async Task ApproveDocumentAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
                throw new Exception("Document not found");

            document.IsApproved = true;
            await _context.SaveChangesAsync();
        }
        public async Task<Document> GetByIdAsync(int id)
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
            var query = _context.Documents
                                            .Include(d => d.User) 
                                            .Include(d => d.Category) 
                                            .Where(d => d.IsApproved) 
                                            .AsQueryable();
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            if (categoryId.HasValue)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            switch (sortBy?.ToLower())
            {
                case "uploadedat_desc": 
                    query = query.OrderByDescending(d => d.UploadedAt);
                    break;
                case "uploadedat_asc":
                    query = query.OrderBy(d => d.UploadedAt);
                    break;
                case "downloadcount_desc": 
                    query = query.OrderByDescending(d => d.DownloadCount);
                    break;
                case "title_asc":
                    query = query.OrderBy(d => d.Title);
                    break;
                case "title_desc":
                    query = query.OrderByDescending(d => d.Title);
                    break;
                default: 
                    query = query.OrderByDescending(d => d.UploadedAt);
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