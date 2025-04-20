using DocumentSharingAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public interface IUserDocumentRepository : IRepository<UserDocument>
    {
        Task<IEnumerable<UserDocument>> GetByUserIdAndActionAsync(int userId, string actionType);
        Task<UserDocument> GetByUserIdDocumentIdAndActionAsync(int userId, int documentId, string actionType);
    }
}