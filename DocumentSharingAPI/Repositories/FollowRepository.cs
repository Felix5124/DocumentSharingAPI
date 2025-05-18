using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public class FollowRepository : Repository<Follow>, IFollowRepository
    {
        public FollowRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<FollowResponseDto>> GetByUserIdAsync(int userId)
        {
            try
            {
                return await _context.Follows
                    .Where(f => f.UserId == userId)
                    .Include(f => f.FollowedUser)
                    .Select(f => new FollowResponseDto
                    {
                        FollowId = f.FollowId,
                        UserId = f.UserId,
                        FollowedUserId = f.FollowedUserId,
                        FollowedUserFullName = f.FollowedUser.FullName,
                        FollowedUserEmail = f.FollowedUser.Email,
                        FollowedAt = f.FollowedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching follows for user {userId}: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<FollowerResponseDto>> GetFollowersByUserIdAsync(int followedUserId)
        {
            try
            {
                return await _context.Follows
                    .Where(f => f.FollowedUserId == followedUserId)
                    .Include(f => f.User)
                    .Select(f => new FollowerResponseDto
                    {
                        FollowId = f.FollowId,
                        UserId = f.UserId,
                        FullName = f.User.FullName,
                        Email = f.User.Email
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching followers for user {followedUserId}: {ex.Message}", ex);
            }
        }

        public async Task<Follow> GetFollowAsync(int userId, int? followedUserId)
        {
            try
            {
                return await _context.Follows
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching follow for user {userId} and followed user {followedUserId}: {ex.Message}", ex);
            }
        }
    }
}