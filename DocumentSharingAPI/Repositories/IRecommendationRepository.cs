using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IRecommendationRepository : IRepository<Recommendation>
    {
        Task<IEnumerable<Document>> GetRecommendedDocumentsAsync(int userId);
    }
}