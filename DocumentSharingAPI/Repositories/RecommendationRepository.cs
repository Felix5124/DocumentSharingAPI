using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class RecommendationRepository : Repository<Recommendation>, IRecommendationRepository
    {
        public RecommendationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Document>> GetRecommendedDocumentsAsync(int userId)
        {
            var userDocs = await _context.Recommendations
                .Where(r => r.UserId == userId)
                .Select(r => r.DocumentId)
                .ToListAsync();

            var categories = await _context.Documents
                .Where(d => userDocs.Contains(d.DocumentId))
                .Select(d => d.CategoryId)
                .Distinct()
                .ToListAsync();

            var recommendedDocs = await _context.Documents
                .Include(d => d.User)
                .Include(d => d.Category)
                .Where(d => d.IsApproved && // Chỉ gợi ý tài liệu đã duyệt
                    categories.Contains(d.CategoryId) &&
                    !userDocs.Contains(d.DocumentId)) // Không gợi ý tài liệu đã tương tác
                .OrderByDescending(d => d.DownloadCount)
                .Take(10)
                .ToListAsync();

            return recommendedDocs;
        }
    }
}