using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IDocumentRepository : IRepository<Document>
    {
        Task<Document> GetByTitleAsync(string title);
        Task<IEnumerable<Document>> SearchAsync(string keyword, int? categoryId, string fileType, string sortBy);
        Task<IEnumerable<Document>> GetPendingDocumentsAsync();
        Task ApproveDocumentAsync(int id);
        Task IncrementDownloadCountAsync(int id);
        Task<(IEnumerable<Document>, int)> GetPagedAsync(int page, int pageSize, string keyword, int? categoryId, string fileType, string sortBy);
        new Task DeleteAsync(int id); // Thêm từ khóa new
        Task<Document> GetTopDownloadedDocumentAsync();
        Task UpdateLockStatusAsync(int documentId, bool isLocked);
    }
}