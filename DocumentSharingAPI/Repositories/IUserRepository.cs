using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByFirebaseUidAsync(string uid);
        Task<User> GetByEmailAsync(string email);
        Task UpdatePointsAsync(int userId, int points);
        Task<IEnumerable<User>> GetTopUsersAsync(int limit);
        Task UpdateLockStatusAsync(int userId, bool isLocked); 
        new Task<User> GetByIdAsync(int id);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByPointsAsync(int limit);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByUploadsAsync(int limit);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByCommentsAsync(int limit);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByDocumentDownloadsAsync(int limit);
        Task<User> GetTopCommenterAsync(); // Thêm phương thức
        Task<User> GetTopPointsUserAsync();
    }
}