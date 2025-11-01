using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByFirebaseUidAsync(string uid);
        Task<User> GetByEmailAsync(string email);
        Task UpdateLockStatusAsync(int userId, bool isLocked); 
        new Task<User> GetByIdAsync(int id);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByUploadsAsync(int limit);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByCommentsAsync(int limit);
        Task<IEnumerable<UserRankingItemDto>> GetTopUsersByDocumentDownloadsAsync(int limit);
        Task<User> GetTopCommenterAsync();
        
        // VIP System methods
        Task ResetDailyDownloadsAsync();
        Task UpdateDownloadCountsAsync(int userId, bool isVipDownload);
        Task<bool> CanDownloadAsync(int userId, bool isVipDocument);
        Task AddVipBonusDownloadAsync(int userId);
        Task AddBonusDownloadAsync(int userId, bool isVipBonus);
    }
}