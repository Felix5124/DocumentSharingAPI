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

        // Points system removed - VIP system used instead

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

        // GetTopUsersAsync removed - Points system deprecated

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
                .GroupBy(x => new { x.user.UserId, x.user.Email, x.user.FullName, x.user.AvatarUrl }) // Thêm FullName, AvatarUrl
                .Select(g => new
                {
                    UserId = g.Key.UserId,
                    Email = g.Key.Email,
                    FullName = g.Key.FullName,
                    AvatarUrl = g.Key.AvatarUrl,
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
                FullName = topCommenter.FullName,
                AvatarUrl = topCommenter.AvatarUrl,
                CommentCount = topCommenter.CommentCount
            };
        }

        // Points system removed - VIP system used instead

        public async Task<IEnumerable<UserRankingItemDto>> GetTopUsersByUploadsAsync(int limit)
        {
            return await _context.Users
                .OrderByDescending(u => u.UploadedDocuments.Count(d => (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "SemiApproved") && !d.IsLock)) // Chỉ đếm tài liệu đã duyệt và không khóa
                .Take(limit)
                .Select(u => new UserRankingItemDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Value = u.UploadedDocuments.Count(d => (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "SemiApproved") && !d.IsLock)
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
                    .Where(d => (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "SemiApproved") && !d.IsLock)
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

        // VIP System methods
        public async Task ResetDailyDownloadsAsync()
        {
            var usersToReset = await _context.Users
                .Where(u => u.LastDownloadResetDate < DateTime.Today)
                .ToListAsync();

            foreach (var user in usersToReset)
            {
                user.VipDownloadsUsedToday = 0;
                user.RegularDownloadsUsedToday = 0;
                user.LastDownloadResetDate = DateTime.Today;
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateDownloadCountsAsync(int userId, bool isVipDownload)
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                if (isVipDownload)
                {
                    // Tài liệu VIP: ưu tiên dùng quota VIP nếu là VIP user, nếu không thì dùng bonus
                    if (user.IsVip && user.VipExpiryDate > DateTime.Now)
                    {
                        user.VipDownloadsUsedToday++;
                    }
                    else if (user.VipBonusDownloads > 0)
                    {
                        user.VipBonusDownloads--;
                    }
                }
                else
                {
                    // Tài liệu thường: ưu tiên dùng quota thường, sau đó mới dùng bonus
                    if ((user.IsVip && user.VipExpiryDate > DateTime.Now) || user.RegularDownloadsUsedToday < 2)
                    {
                        user.RegularDownloadsUsedToday++;
                    }
                    else if (user.RegularBonusDownloads > 0)
                    {
                        user.RegularBonusDownloads--;
                    }
                }
                await UpdateAsync(user);
            }
        }

        public async Task<bool> CanDownloadAsync(int userId, bool isVipDocument)
        {
            var user = await CheckAndResetDailyLimitsAsync(userId);

            if (user == null) return false;

            // Reset daily downloads if needed
            if (user.LastDownloadResetDate < DateTime.Today)
            {
                user.VipDownloadsUsedToday = 0;
                user.RegularDownloadsUsedToday = 0;
                user.LastDownloadResetDate = DateTime.Today;
                await UpdateAsync(user);
            }

            if (isVipDocument)
            {
                // Tài liệu VIP chỉ VIP user hoặc có bonus downloads VIP mới tải được
                if (user.IsVip && user.VipExpiryDate > DateTime.Now)
                {
                    return user.VipDownloadsUsedToday < 10; // VIP: 10 lượt tải VIP/ngày
                }
                else
                {
                    return user.VipBonusDownloads > 0; // Thường: cần có bonus download VIP
                }
            }
            else
            {
                // Tài liệu thường
                if (user.IsVip && user.VipExpiryDate > DateTime.Now)
                {
                    return user.RegularDownloadsUsedToday < 10; // VIP: 10 lượt tải thường/ngày
                }
                else
                {
                    // Tài khoản thường: 2 lượt cơ bản + bonus downloads thường
                    return (user.RegularDownloadsUsedToday < 2) || (user.RegularBonusDownloads > 0);
                }
            }
        }

        public async Task AddVipBonusDownloadAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                user.VipBonusDownloads++;
                await UpdateAsync(user);
            }
        }

        public async Task AddBonusDownloadAsync(int userId, bool isVipBonus)
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                if (isVipBonus)
                {
                    user.VipBonusDownloads++;
                }
                else
                {
                    user.RegularBonusDownloads++;
                }
                await UpdateAsync(user);
            }
        }

        public async Task<User> CheckAndResetDailyLimitsAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            if (user == null) return null;

            // Kiểm tra nếu ngày reset cuối cùng nhỏ hơn hôm nay
            if (user.LastDownloadResetDate.Date < DateTime.Today)
            {
                user.VipDownloadsUsedToday = 0;
                user.RegularDownloadsUsedToday = 0;
                user.LastDownloadResetDate = DateTime.Today;

                // Lưu thay đổi xuống database ngay lập tức
                await UpdateAsync(user);
            }

            return user;
        }

    }
}