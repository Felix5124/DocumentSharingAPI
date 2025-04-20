using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class CommentRepository : Repository<Comment>, ICommentRepository
    {
        public CommentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Comment>> GetByDocumentIdAsync(int documentId)
        {
            return await _context.Comments
                .Where(c => c.DocumentId == documentId)
                .Include(c => c.User)
                .ToListAsync();
        }
    }
}