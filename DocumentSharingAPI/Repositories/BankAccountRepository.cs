using DocumentSharingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Repositories
{
    public class BankAccountRepository : Repository<BankAccount>, IBankAccountRepository
    {
        public BankAccountRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<BankAccount?> GetDefaultBankAccountAsync()
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(b => b.IsDefault && b.IsActive);
        }

        public async Task<List<BankAccount>> GetActiveBankAccountsAsync()
        {
            return await _context.BankAccounts
                .Where(b => b.IsActive)
                .ToListAsync();
        }
    }
}
