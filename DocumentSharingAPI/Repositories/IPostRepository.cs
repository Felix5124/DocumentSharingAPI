using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IPostRepository : IRepository<Post>
    {
        Task<IEnumerable<Post>> GetAllWithCommentsAsync();
    }
}