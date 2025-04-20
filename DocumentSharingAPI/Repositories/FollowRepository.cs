using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class FollowRepository : Repository<Follow>, IFollowRepository
    {
        public FollowRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Follow>> GetByUserIdAsync(int userId)
        {
            return await _context.Follows
                .Where(f => f.UserId == userId)
                .Include(f => f.FollowedUser)
                .Include(f => f.Category)
                .ToListAsync();
        }

        public async Task<Follow> GetFollowAsync(int userId, int? followedUserId, int? categoryId)
        {
            return await _context.Follows
                .FirstOrDefaultAsync(f => f.UserId == userId &&
                    (followedUserId == null || f.FollowedUserId == followedUserId) &&
                    (categoryId == null || f.CategoryId == categoryId));
        }
    }
}