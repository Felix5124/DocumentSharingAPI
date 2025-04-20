using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByFirebaseUidAsync(string uid);
        Task<User> GetByEmailAsync(string email);
        Task UpdatePointsAsync(int userId, int points);
        Task<IEnumerable<User>> GetTopUsersAsync(int limit);
    }
}