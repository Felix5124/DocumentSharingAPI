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
    }
}
