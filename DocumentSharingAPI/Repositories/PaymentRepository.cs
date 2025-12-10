using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        public PaymentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Payment?> GetByOrderCodeAsync(string orderCode)
        {
            return await _context.Payments
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.OrderCode == orderCode);
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync()
        {
            return await _context.Payments
                .Include(p => p.User)
                .Where(p => p.Status == "Pending")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Payment>> GetPaymentsByUserIdAsync(int userId)
        {
            return await _context.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Payment>> GetExpiredPaymentsAsync()
        {
            var now = DateTime.Now;
            return await _context.Payments
                .Where(p => p.Status == "Pending" && p.ExpiredAt <= now)
                .ToListAsync();
        }

        public async Task<Payment?> GetPaymentWithUserAsync(int paymentId)
        {
            return await _context.Payments
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        }

        public async Task<(List<Payment> Items, int TotalCount)> GetAdminPaymentsAsync(
            int page,
            int pageSize,
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _context.Payments
                .Include(p => p.User)
                .AsQueryable();

            // 1. Lọc theo từ khóa (Mã đơn, Tên user, Email user)
            if (!string.IsNullOrEmpty(keyword))
            {
                string kw = keyword.ToLower();
                query = query.Where(p =>
                    p.OrderCode.ToLower().Contains(kw) ||
                    (p.User != null && (p.User.FullName.ToLower().Contains(kw) || p.User.Email.ToLower().Contains(kw)))
                );
            }

            // 2. Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                if (status == "Expired")
                {
                    // Lấy các đơn có status là "Expired" 
                    query = query.Where(p => p.Status == "Expired" || (p.Status == "Pending" && p.ExpiredAt <= DateTime.Now));
                }
                else if (status == "Pending")
                {
                    // Chỉ lấy các đơn "Pending" và VẪN CÒN hạn
                    query = query.Where(p => p.Status == "Pending" && p.ExpiredAt > DateTime.Now);
                }
                else
                {
                    // Các trạng thái khác (Completed, Cancelled) giữ nguyên
                    query = query.Where(p => p.Status == status);
                }
            }

            // 3. Lọc theo ngày (Từ ngày - Đến ngày)
            if (fromDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= fromDate.Value.Date);
            }
            if (toDate.HasValue)
            {
                // Đến cuối ngày đó (23:59:59)
                query = query.Where(p => p.CreatedAt <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            // Đếm tổng số trước khi phân trang
            int totalCount = await query.CountAsync();

            // 4. Sắp xếp và Phân trang
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
