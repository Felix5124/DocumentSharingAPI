using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class UserBadgeRepository : Repository<UserBadge>, IUserBadgeRepository
    {
        public UserBadgeRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<UserBadge>> GetByUserIdAsync(int userId)
        {
            return await _context.UserBadges
                .Where(ub => ub.UserId == userId)
                .Include(ub => ub.Badge)
                .ToListAsync();
        }
    }
}