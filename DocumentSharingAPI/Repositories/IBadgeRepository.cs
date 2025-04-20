using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IBadgeRepository : IRepository<Badge>
    {
        Task<Badge> GetByNameAsync(string name);
    }
}