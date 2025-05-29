using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface ITagRepository
    {
        Task<Tag> GetByIdAsync(int id);
        Task<Tag> GetByNameAsync(string name);
        Task<IEnumerable<Tag>> GetAllAsync();
        Task<Tag> AddAsync(Tag tag);
        Task<Tag> GetOrCreateTagAsync(string tagName);
        Task UpdateAsync(Tag tag);
        Task DeleteAsync(int id);
        Task<IEnumerable<Tag>> GetTagsByDocumentIdAsync(int documentId);
    }
}
