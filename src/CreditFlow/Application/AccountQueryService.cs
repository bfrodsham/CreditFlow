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
        var accountExists = await _dbContext.Accounts
            .AnyAsync(account => account.Id == accountId);

        if (!accountExists)
        {
            return null;
        }

        var balance = await CreditLedgerService.GetBalanceAsync(_dbContext.CreditLedgerEntries, accountId);
        return new AccountBalanceDto(accountId, balance);
    }
}
