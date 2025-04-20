using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class PostRepository : Repository<Post>, IPostRepository
    {
        public PostRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Post>> GetAllWithCommentsAsync()
        {
            return await _context.Posts
                .Include(p => p.Comments)
                .Include(p => p.User)
                .ToListAsync();
        }
    }
}