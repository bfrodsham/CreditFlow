using CreditFlow.Application;
using CreditFlow.Domain;

namespace CreditFlow.Tests;

public class CreditLedgerServiceTests
{
    [Fact]
    public void GetBalance_SumsAcrossMultipleEntryTypes()
    {
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();

        var entries = new List<CreditLedgerEntry>
        {
            BuildEntry(accountId, 100, CreditLedgerEntryType.Grant),
            BuildEntry(accountId, -30, CreditLedgerEntryType.Consume),
            BuildEntry(accountId, -10, CreditLedgerEntryType.Expire),
            BuildEntry(accountId, 5, CreditLedgerEntryType.Rollover),
            BuildEntry(otherAccountId, 999, CreditLedgerEntryType.Grant)
        };

        var sut = new CreditLedgerService(entries);

        var balance = sut.GetBalance(accountId);

        Assert.Equal(65, balance);
    }

    [Fact]
    public void GetBalance_ReturnsZero_WhenAccountHasNoEntries()
    {
        var accountId = Guid.NewGuid();

        var entries = new List<CreditLedgerEntry>
        {
            BuildEntry(Guid.NewGuid(), 40, CreditLedgerEntryType.Grant)
        };

        var sut = new CreditLedgerService(entries);

        var balance = sut.GetBalance(accountId);

        Assert.Equal(0, balance);
    }

    [Fact]
    public void GetBalance_HandlesMixedPositiveAndNegativeEntries()
    {
        var accountId = Guid.NewGuid();

        var entries = new List<CreditLedgerEntry>
        {
            BuildEntry(accountId, -25, CreditLedgerEntryType.Consume),
            BuildEntry(accountId, 200, CreditLedgerEntryType.Grant),
            BuildEntry(accountId, -50, CreditLedgerEntryType.Consume),
            BuildEntry(accountId, 15, CreditLedgerEntryType.Adjustment),
            BuildEntry(accountId, -20, CreditLedgerEntryType.Expire)
        };

        var sut = new CreditLedgerService(entries);

        var balance = sut.GetBalance(accountId);

        Assert.Equal(120, balance);
    }

    private static CreditLedgerEntry BuildEntry(Guid accountId, int amount, CreditLedgerEntryType type)
    {
        return new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Type = type,
            Source = CreditLedgerEntrySource.ManualAdjustment,
            IdempotencyKey = Guid.NewGuid().ToString(),
            ReferenceId = Guid.NewGuid().ToString(),
            Description = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
