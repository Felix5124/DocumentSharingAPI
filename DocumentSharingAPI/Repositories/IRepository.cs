namespace DocumentSharingAPI.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> GetByIdAsync(int id);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(int id);

        // --- TRANSACTION SUPPORT METHODS ---
        Task AddForTransactionAsync(T entity); // Add to context without saving
        Task UpdateForTransactionAsync(T entity); // Update in context without saving
        Task DeleteForTransactionAsync(int id); // Delete from context without saving
    }
}