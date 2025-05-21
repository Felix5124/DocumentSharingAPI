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

        public async Task<IEnumerable<UserRankingItemDto>> GetTopUsersByPointsAsync(int limit)
        {
            return await _context.Users
                .OrderByDescending(u => u.Points)
                .Take(limit)
                .Select(u => new UserRankingItemDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Value = u.Points
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<UserRankingItemDto>> GetTopUsersByUploadsAsync(int limit)
        {
            return await _context.Users
                .OrderByDescending(u => u.UploadedDocuments.Count(d => d.IsApproved && !d.IsLock)) // Chỉ đếm tài liệu đã duyệt và không khóa
                .Take(limit)
                .Select(u => new UserRankingItemDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Value = u.UploadedDocuments.Count(d => d.IsApproved && !d.IsLock)
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<UserRankingItemDto>> GetTopUsersByCommentsAsync(int limit)
        {
            return await _context.Comments
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, CommentCount = g.Count() })
                .OrderByDescending(x => x.CommentCount)
                .Take(limit)
                .Join(_context.Users,
                      commentGroup => commentGroup.UserId,
                      user => user.UserId,
                      (commentGroup, user) => new UserRankingItemDto
                      {
                          UserId = user.UserId,
                          FullName = user.FullName,
                          Email = user.Email,
                          AvatarUrl = user.AvatarUrl,
                          Value = commentGroup.CommentCount
                      })
                .ToListAsync();
        }

        public async Task<IEnumerable<UserRankingItemDto>> GetTopUsersByDocumentDownloadsAsync(int limit)
        {
            try
            {
                // Bước 1: Tính toán tổng lượt tải cho mỗi user
                var userDownloadStats = _context.Documents
                    .Where(d => d.IsApproved && !d.IsLock)
                    .GroupBy(d => d.UploadedBy) // Group theo UserId của người tải lên
                    .Select(g => new
                    {
                        UserId = g.Key,
                        TotalDownloads = g.Sum(doc => doc.DownloadCount) // Đảm bảo xử lý null cho DownloadCount
                    })
                    .OrderByDescending(x => x.TotalDownloads)
                    .Take(limit); // Lấy top N user dựa trên lượt tải

                // Bước 2: Join kết quả với bảng Users để lấy thông tin chi tiết
                // và chiếu (project) vào UserRankingItemDto
                var result = await userDownloadStats
                    .Join(
                        _context.Users, // Bảng Users
                        stat => stat.UserId, // Khóa từ userDownloadStats (là UploadedBy)
                        user => user.UserId,  // Khóa từ Users
                        (stat, user) => new UserRankingItemDto // Kết quả sau khi join
                        {
                            UserId = user.UserId,
                            FullName = user.FullName,
                            Email = user.Email,
                            AvatarUrl = user.AvatarUrl,
                            Value = stat.TotalDownloads
                        }
                    )
                    .ToListAsync(); // Thực thi truy vấn và lấy kết quả

                // Vì Join có thể thay đổi thứ tự, nếu cần đảm bảo thứ tự chính xác theo TotalDownloads,
                // bạn có thể sắp xếp lại ở client hoặc sắp xếp lại kết quả cuối cùng này.
                // Tuy nhiên, OrderByDescending ở userDownloadStats thường đã đủ.
                // Nếu muốn chắc chắn, có thể thêm: result = result.OrderByDescending(r => r.Value).ToList();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetTopUsersByDocumentDownloadsAsync: {ex.ToString()}");
                // Log lỗi này (ví dụ: sử dụng ILogger)
                throw; // Ném lại lỗi để controller xử lý
            }
        }
    }
}