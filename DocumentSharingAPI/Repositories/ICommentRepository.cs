using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface ICommentRepository : IRepository<Comment>
    {
        Task<IEnumerable<Comment>> GetByDocumentIdAsync(int documentId);
    }
}