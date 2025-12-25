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

        // Upload Limit System
        public async Task<User> CheckAndResetUploadLimitsAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            // Reset nếu qua ngày mới
            if (user.LastUploadResetDate.Date < DateTime.Today)
            {
                user.RegularUploadsUsedToday = 0;
                user.VipUploadsUsedToday = 0;
                user.LastUploadResetDate = DateTime.Today;
                await _context.SaveChangesAsync();
            }

            return user;
        }

        public async Task<bool> CanUploadAsync(int userId, bool isVipDocument)
        {
            var user = await CheckAndResetUploadLimitsAsync(userId);
            if (user == null) return false;

            bool isVipActive = user.IsVip && (!user.VipExpiryDate.HasValue || user.VipExpiryDate.Value > DateTime.Now);

            // VIP document chỉ VIP mới upload được
            if (isVipDocument && !isVipActive) return false;

            // User thường: chỉ upload được 2 file/ngày (chỉ đếm file thường)
            if (!isVipActive)
            {
                // Non-VIP users cannot upload VIP documents (checked above),
                // so only count regular uploads for their limit.
                return user.RegularUploadsUsedToday < 2; // allow up to 2 regular uploads per day
            }

            // User VIP: tổng cộng 5 file/ngày (bao gồm cả VIP và regular)
            // VIP users may upload both types; count both counters toward the total cap.
            return (user.RegularUploadsUsedToday + user.VipUploadsUsedToday) < 5;
        }

        public async Task UpdateUploadCountsAsync(int userId, bool isVipUpload)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            if (isVipUpload)
                user.VipUploadsUsedToday++;
            else
                user.RegularUploadsUsedToday++;
            
            await _context.SaveChangesAsync();
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
                    if (user.IsVip && user.VipExpiryDate > DateTime.Now && user.VipDownloadsUsedToday < 5)
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
                    int dailyLimit = (user.IsVip && user.VipExpiryDate > DateTime.Now) ? 8 : 2;
                    
                    if (user.RegularDownloadsUsedToday < dailyLimit)
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
                    // VIP: 5 lượt VIP daily + bonus VIP
                    return (user.VipDownloadsUsedToday < 5) || (user.VipBonusDownloads > 0);
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
                    // VIP: 8 lượt thường daily + bonus thường
                    return (user.RegularDownloadsUsedToday < 8) || (user.RegularBonusDownloads > 0);
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

        public async Task<(IEnumerable<User>, int)> GetAdminUsersAsync(int page, int pageSize, string keyword, bool? isLocked, string? role)
        {
            var query = _context.Users.AsQueryable();

            // 1. Lọc theo từ khóa (Họ tên hoặc Email)
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u => u.FullName.Contains(keyword) || u.Email.Contains(keyword));
            }

            // 2. Lọc theo trạng thái Khóa
            if (isLocked.HasValue)
            {
                query = query.Where(u => u.IsLocked == isLocked.Value);
            }

            // 3. Lọc theo Vai trò (Admin / VIP / User)
            if (!string.IsNullOrEmpty(role))
            {
                switch (role.ToLower())
                {
                    case "admin":
                        query = query.Where(u => u.IsAdmin == true);
                        break;
                    case "vip":
                        query = query.Where(u => u.IsVip == true); // Giả sử logic VIP là IsVip = true
                        break;
                    case "regular": // Người dùng thường
                        query = query.Where(u => u.IsAdmin == false && u.IsVip == false);
                        break;
                }
            }

            // Đếm tổng số bản ghi trước khi phân trang
            int totalCount = await query.CountAsync();

            // Sắp xếp mặc định (Mới nhất lên đầu hoặc theo ID)
            query = query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.UserId);

            // Phân trang
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

    }
}