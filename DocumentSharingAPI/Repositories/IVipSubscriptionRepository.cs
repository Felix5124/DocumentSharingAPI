using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IVipSubscriptionRepository : IRepository<VipSubscription>
    {
        Task<VipSubscription?> GetActiveSubscriptionByUserIdAsync(int userId);
        Task<IEnumerable<VipSubscription>> GetSubscriptionsByUserIdAsync(int userId);
        Task<bool> HasActiveVipSubscriptionAsync(int userId);
        Task DeactivateExpiredSubscriptionsAsync();
    }
}