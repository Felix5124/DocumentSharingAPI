using DocumentSharingAPI.Models;

namespace DocumentSharingAPI.Repositories
{
    public interface IBankAccountRepository : IRepository<BankAccount>
    {
        Task<BankAccount?> GetDefaultBankAccountAsync();
        Task<List<BankAccount>> GetActiveBankAccountsAsync();
    }
}
