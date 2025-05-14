using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IPostRepository : IRepository<Post>
    {
        Task<Post> GetByIdWithDetailsAsync(int postId);
        Task<IEnumerable<Post>> GetAllWithCommentsAsync();
    }
}