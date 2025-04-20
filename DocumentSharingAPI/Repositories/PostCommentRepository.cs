using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class PostCommentRepository : Repository<PostComment>, IPostCommentRepository
    {
        public PostCommentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PostComment>> GetByPostIdAsync(int postId)
        {
            return await _context.PostComments
                .Where(pc => pc.PostId == postId)
                .Include(pc => pc.User)
                .ToListAsync();
        }
    }
}