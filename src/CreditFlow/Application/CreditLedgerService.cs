using CreditFlow.Domain;

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
}
