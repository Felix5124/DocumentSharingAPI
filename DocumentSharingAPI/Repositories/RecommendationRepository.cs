using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Repositories
{
    public class RecommendationRepository : Repository<Recommendation>, IRecommendationRepository
    {
        public RecommendationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Document>> GetRecommendedDocumentsAsync(int userId)
        {
            try
            {
                // Lấy danh sách DocumentId mà người dùng đã tương tác
                var userDocIds = await _context.Recommendations
                    .Where(r => r.UserId == userId)
                    .Select(r => r.DocumentId)
                    .ToListAsync();

                // Lấy danh sách tài liệu mà người dùng đã tương tác (để lấy CategoryId)
                var userDocs = await _context.Documents
                    .Where(d => userDocIds.Contains(d.DocumentId))
                    .ToListAsync();

                // Lấy danh sách danh mục từ các tài liệu đã tương tác
                var categories = userDocs.Select(d => d.CategoryId).Distinct().ToList();

                // Nếu không có danh mục nào (người dùng chưa tương tác), trả về tài liệu phổ biến
                if (!categories.Any())
                {
                    return await _context.Documents
                        .Where(d => d.IsApproved == true) // Chỉ lấy tài liệu đã được duyệt
                        .OrderByDescending(d => d.DownloadCount) // Sắp xếp theo lượt tải
                        .Take(10)
                        .ToListAsync();
                }

                // Đề xuất tài liệu từ các danh mục đã tương tác
                var recommendedDocs = await _context.Documents
                    .Where(d =>
                        categories.Contains(d.CategoryId) && // Thuộc danh mục đã tương tác
                        !userDocIds.Contains(d.DocumentId) && // Chưa từng tương tác
                        d.IsApproved == true) // Đã được duyệt
                    .Take(10)
                    .ToListAsync();

                return recommendedDocs;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching recommended documents for user {userId}: {ex.Message}", ex);
            }
        }
    }
}