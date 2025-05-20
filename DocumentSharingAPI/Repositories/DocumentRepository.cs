using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public class DocumentRepository : Repository<Document>, IDocumentRepository
    {
        public DocumentRepository(AppDbContext context) : base(context)
        {
        }

        public new async Task DeleteAsync(int id) // Thêm từ khóa new
        {
            try
            {
                var document = await _context.Documents.FindAsync(id);
                if (document == null)
                {
                    Console.WriteLine($"Document with ID {id} not found in DeleteAsync.");
                    return;
                }

                var notifications = await _context.Notifications
                    .Where(n => n.DocumentId == id)
                    .ToListAsync();
                if (notifications.Any())
                {
                    _context.Notifications.RemoveRange(notifications);
                    Console.WriteLine($"Deleted {notifications.Count} notifications for DocumentId {id}");
                }

                var userDocuments = await _context.UserDocuments
                    .Where(ud => ud.DocumentId == id)
                    .ToListAsync();
                if (userDocuments.Any())
                {
                    _context.UserDocuments.RemoveRange(userDocuments);
                    Console.WriteLine($"Deleted {userDocuments.Count} user documents for DocumentId {id}");
                }

                var comments = await _context.Comments
                    .Where(c => c.DocumentId == id)
                    .ToListAsync();
                if (comments.Any())
                {
                    _context.Comments.RemoveRange(comments);
                    Console.WriteLine($"Deleted {comments.Count} comments for DocumentId {id}");
                }

                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Document with ID {id} deleted from database.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteAsync for DocumentId {id}: {ex.Message}");
                throw;
            }
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

            query = query.Where(d => d.IsApproved == true);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            if (categoryId.HasValue && categoryId.Value > 0)
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

        public async Task<int> GetTotalCountAsync(string keyword, int? categoryId, string fileType, bool isApproved = false)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(d => d.Title.Contains(keyword) || d.Description.Contains(keyword));

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(d => d.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(fileType))
                query = query.Where(d => d.FileType == fileType);

            if (isApproved)
            {
                query = query.Where(d => d.IsApproved == true);
            }

            return await query.CountAsync();
        }

        public async Task UpdateLockStatusAsync(int documentId, bool isLocked)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                Console.WriteLine($"Document with ID {documentId} not found.");
                throw new Exception("Document not found");
            }

            Console.WriteLine($"Updating lock status for document ID {documentId}: IsLocked = {isLocked}");
            document.IsLock = isLocked;
            await _context.SaveChangesAsync();
            Console.WriteLine($"Lock status updated for document ID {documentId}: IsLocked = {document.IsLock}");
        }

        public async Task<Document> GetTopDownloadedDocumentAsync()
        {
            return await _context.Documents
                .Where(d => d.IsApproved == true)
                .OrderByDescending(d => d.DownloadCount)
                .Select(d => new Document
                {
                    DocumentId = d.DocumentId,
                    Title = d.Title,
                    DownloadCount = d.DownloadCount
                })
                .FirstOrDefaultAsync();
        }
    }
}