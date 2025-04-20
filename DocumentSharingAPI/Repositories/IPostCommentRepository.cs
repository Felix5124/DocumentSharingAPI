using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IPostCommentRepository : IRepository<PostComment>
    {
        Task<IEnumerable<PostComment>> GetByPostIdAsync(int postId);
    }
}