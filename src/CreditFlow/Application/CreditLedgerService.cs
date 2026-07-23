using CreditFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Application;

public class CreditLedgerService
{
    private readonly IReadOnlyCollection<CreditLedgerEntry> _entries;

    public CreditLedgerService(IReadOnlyCollection<CreditLedgerEntry> entries)
    {
        _entries = entries;
    }

    public int GetBalance(Guid accountId)
    {
        return _entries
            .Where(entry => entry.AccountId == accountId)
            .Sum(entry => entry.Amount);
    }

    public static async Task<int> GetBalanceAsync(IQueryable<CreditLedgerEntry> entries, Guid accountId, CancellationToken cancellationToken = default)
    {
        return await entries
            .Where(entry => entry.AccountId == accountId)
            .SumAsync(entry => (int?)entry.Amount, cancellationToken) ?? 0;
    }
}
