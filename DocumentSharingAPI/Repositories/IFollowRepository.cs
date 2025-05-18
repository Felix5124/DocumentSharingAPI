using DocumentSharingAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public interface IFollowRepository : IRepository<Follow>
    {
        Task<IEnumerable<FollowResponseDto>> GetByUserIdAsync(int userId); // Sửa thành FollowResponseDto
        Task<IEnumerable<FollowerResponseDto>> GetFollowersByUserIdAsync(int followedUserId);
        Task<Follow> GetFollowAsync(int userId, int? followedUserId);
    }
}