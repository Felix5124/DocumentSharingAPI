using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class UserDocumentRepository : Repository<UserDocument>, IUserDocumentRepository
    {
        public UserDocumentRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<UserDocument>> GetByUserIdAndActionAsync(int userId, string actionType)
        {
            return await _context.UserDocuments
                .Include(ud => ud.Document)
                .Where(ud => ud.UserId == userId && ud.ActionType == actionType)
                .ToListAsync();
        }

        public async Task<UserDocument> GetByUserIdDocumentIdAndActionAsync(int userId, int documentId, string actionType)
        {
            return await _context.UserDocuments
                .Include(ud => ud.Document)
                .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.DocumentId == documentId && ud.ActionType == actionType);
        }
    }
}