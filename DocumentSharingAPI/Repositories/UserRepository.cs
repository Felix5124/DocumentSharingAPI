using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

            if (user.Points >= 1000)
                user.Level = "Master";
            else if (user.Points >= 500)
                user.Level = "Scholar";
            else
                user.Level = "Newbie";

            await _context.SaveChangesAsync();
        }

        public async Task UpdateLockStatusAsync(int userId, bool isLocked)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Console.WriteLine($"User with ID {userId} not found.");
                throw new Exception("User not found");
            }

            Console.WriteLine($"Updating lock status for user ID {userId}: IsLocked = {isLocked}");
            user.IsLocked = isLocked;
            await _context.SaveChangesAsync();
            Console.WriteLine($"Lock status updated for user ID {userId}: IsLocked = {user.IsLocked}");
        }

        public async Task<IEnumerable<User>> GetTopUsersAsync(int limit)
        {
            return await _context.Users
                .OrderByDescending(u => u.Points)
                .ThenByDescending(u => u.UploadedDocuments.Count)
                .Take(limit)
                .ToListAsync();
        }

        public new async Task<User> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        // Thêm phương thức: Người có nhiều comment nhất
        public async Task<User> GetTopCommenterAsync()
        {
            var topCommenter = await _context.Users
                .Join(_context.Comments,
                      user => user.UserId,
                      comment => comment.UserId,
                      (user, comment) => new { user, comment })
                .GroupBy(x => new { x.user.UserId, x.user.Email })
                .Select(g => new
                {
                    UserId = g.Key.UserId,
                    Email = g.Key.Email,
                    CommentCount = g.Count()
                })
                .OrderByDescending(x => x.CommentCount)
                .FirstOrDefaultAsync();

            if (topCommenter == null)
                return null;

            return new User
            {
                UserId = topCommenter.UserId,
                Email = topCommenter.Email,
                CommentCount = topCommenter.CommentCount
            };
        }

        // Thêm phương thức: Người có nhiều điểm nhất
        public async Task<User> GetTopPointsUserAsync()
        {
            return await _context.Users
                .OrderByDescending(u => u.Points)
                .Select(u => new User
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    Points = u.Points
                })
                .FirstOrDefaultAsync();
        }
    }
}