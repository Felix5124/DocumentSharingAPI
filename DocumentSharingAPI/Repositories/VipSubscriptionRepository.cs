using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class VipSubscriptionRepository : Repository<VipSubscription>, IVipSubscriptionRepository
    {
        public VipSubscriptionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<VipSubscription?> GetActiveSubscriptionByUserIdAsync(int userId)
        {
            return await _context.VipSubscriptions
                .Where(vs => vs.UserId == userId && vs.IsActive && vs.EndDate > DateTime.Now)
                .OrderByDescending(vs => vs.EndDate)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<VipSubscription>> GetSubscriptionsByUserIdAsync(int userId)
        {
            return await _context.VipSubscriptions
                .Where(vs => vs.UserId == userId)
                .OrderByDescending(vs => vs.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasActiveVipSubscriptionAsync(int userId)
        {
            return await _context.VipSubscriptions
                .AnyAsync(vs => vs.UserId == userId && vs.IsActive && vs.EndDate > DateTime.Now);
        }

        public async Task DeactivateExpiredSubscriptionsAsync()
        {
            var expiredSubscriptions = await _context.VipSubscriptions
                .Where(vs => vs.IsActive && vs.EndDate <= DateTime.Now)
                .ToListAsync();

            foreach (var subscription in expiredSubscriptions)
            {
                subscription.IsActive = false;
            }

            await _context.SaveChangesAsync();
        }
    }
}