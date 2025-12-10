using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<Payment?> GetByOrderCodeAsync(string orderCode);
        Task<List<Payment>> GetPendingPaymentsAsync();
        Task<List<Payment>> GetPaymentsByUserIdAsync(int userId);
        Task<List<Payment>> GetExpiredPaymentsAsync();
        Task<Payment?> GetPaymentWithUserAsync(int paymentId);
        
        // Thêm hàm này
        Task<(List<Payment> Items, int TotalCount)> GetAdminPaymentsAsync(
            int page,
            int pageSize,
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate);
    }
}
