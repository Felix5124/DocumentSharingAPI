using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

namespace DocumentSharingAPI.Repositories
{
    public class PostRepository : Repository<Post>, IPostRepository
    {
        public PostRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Post> GetByIdWithDetailsAsync(int postId)
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(pc => pc.User) // Nếu cần thông tin người comment
                .FirstOrDefaultAsync(p => p.PostId == postId);
        }


        public async Task<IEnumerable<Post>> GetAllWithCommentsAsync()
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .ToListAsync();
        }
    }
}