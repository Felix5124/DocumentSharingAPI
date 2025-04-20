using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IFollowRepository : IRepository<Follow>
    {
        Task<IEnumerable<Follow>> GetByUserIdAsync(int userId);
        Task<Follow> GetFollowAsync(int userId, int? followedUserId, int? categoryId);
    }
}