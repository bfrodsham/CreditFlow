using CreditFlow.Contracts;
using CreditFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Application;

public class AccountQueryService
{
    private readonly CreditFlowDbContext _dbContext;

    public AccountQueryService(CreditFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AccountSummaryDto>> GetAccountsAsync()
    {
        return await _dbContext.Accounts
            .OrderBy(account => account.Name)
            .Select(account => new AccountSummaryDto(account.Id, account.Name, account.Email))
            .ToListAsync();
    }

    public async Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId)
    {
        return await _dbContext.Accounts
            .Where(account => account.Id == accountId)
            .Select(account => new AccountBalanceDto(
                account.Id,
                _dbContext.CreditLedgerEntries
                    .Where(entry => entry.AccountId == account.Id)
                    .Sum(entry => (int?)entry.Amount) ?? 0))
            .SingleOrDefaultAsync();
    }
}
