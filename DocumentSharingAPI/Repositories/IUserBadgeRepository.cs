using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IUserBadgeRepository : IRepository<UserBadge>
    {
        Task<IEnumerable<UserBadge>> GetByUserIdAsync(int userId);
    }
}