using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<Category> GetByNameAsync(string name);
    }
}