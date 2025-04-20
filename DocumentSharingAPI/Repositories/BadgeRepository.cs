using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class BadgeRepository : Repository<Badge>, IBadgeRepository
    {
        public BadgeRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Badge> GetByNameAsync(string name)
        {
            return await _context.Badges.FirstOrDefaultAsync(b => b.Name == name);
        }
    }
}