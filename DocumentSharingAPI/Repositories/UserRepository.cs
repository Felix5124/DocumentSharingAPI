using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<User> GetByFirebaseUidAsync(string uid)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == uid);
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task UpdatePointsAsync(int userId, int points)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            user.Points += points;
            if (user.Points < 0)
                user.Points = 0;

            // Logic cấp bậc
            if (user.Points >= 1000)
                user.Level = "Master";
            else if (user.Points >= 500)
                user.Level = "Scholar";
            else
                user.Level = "Newbie";

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<User>> GetTopUsersAsync(int limit)
        {
            return await _context.Users
                .OrderByDescending(u => u.Points)
                .ThenByDescending(u => u.UploadedDocuments.Count)
                .Take(limit)
                .ToListAsync();
        }
    }
}