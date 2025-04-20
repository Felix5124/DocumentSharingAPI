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
                .Select(r => r.Document)
                .ToListAsync();

            var categories = userDocs.Select(d => d.CategoryId).Distinct();
            var recommendedDocs = await _context.Documents
                .Where(d => categories.Contains(d.CategoryId) && !userDocs.Contains(d))
                .Take(10)
                .ToListAsync();

            return recommendedDocs;
        }
    }
}