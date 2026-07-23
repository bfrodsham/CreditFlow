using CreditFlow.Application;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Tests;

public class UsageServiceTests
{
    [Fact]
    public async Task RecordUsageEvent_DebitsBalanceAndWritesUsageEvent()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();

        testContext.DbContext.Accounts.Add(new Account
        {
            Id = accountId,
            Name = "Acme",
            Email = "acme@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 100, "grant-1", CreditLedgerEntryType.Grant));
        await testContext.DbContext.SaveChangesAsync();

        var sut = new UsageService(testContext.DbContext);

        var result = await sut.RecordUsageEvent(accountId, "model-inference", "usage-1");

        Assert.Equal(25, result.CreditCost);
        Assert.Equal(75, result.Balance);
        Assert.Equal(1, await testContext.DbContext.UsageEvents.CountAsync());

        var ledgerEntry = await testContext.DbContext.CreditLedgerEntries.SingleAsync(entry => entry.IdempotencyKey == "usage-1");
        Assert.Equal(-25, ledgerEntry.Amount);
        Assert.Equal(CreditLedgerEntryType.Consume, ledgerEntry.Type);
        Assert.Equal(CreditLedgerEntrySource.UsageEvent, ledgerEntry.Source);
    }

    [Fact]
    public async Task RecordUsageEvent_ThrowsWhenBalanceIsInsufficient_AndWritesNoEntry()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();

        testContext.DbContext.Accounts.Add(new Account
        {
            Id = accountId,
            Name = "Bluebird",
            Email = "bluebird@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 10, "grant-1", CreditLedgerEntryType.Grant));
        await testContext.DbContext.SaveChangesAsync();

        var sut = new UsageService(testContext.DbContext);

        var exception = await Assert.ThrowsAsync<InsufficientCreditsException>(() => sut.RecordUsageEvent(accountId, "model-inference", "usage-2"));

        Assert.Equal(10, exception.AvailableCredits);
        Assert.Equal(25, exception.RequiredCredits);
        Assert.Equal(0, await testContext.DbContext.UsageEvents.CountAsync(usageEvent => usageEvent.AccountId == accountId));
        Assert.Equal(1, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId));
    }

    [Fact]
    public async Task RecordUsageEvent_ReturnsExistingResult_WhenIdempotencyKeyAlreadyExists()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();

        testContext.DbContext.Accounts.Add(new Account
        {
            Id = accountId,
            Name = "Northwind",
            Email = "northwind@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 100, "grant-1", CreditLedgerEntryType.Grant));
        await testContext.DbContext.SaveChangesAsync();

        var sut = new UsageService(testContext.DbContext);

        var firstResult = await sut.RecordUsageEvent(accountId, "api-call", "usage-3");
        var secondResult = await sut.RecordUsageEvent(accountId, "api-call", "usage-3");

        Assert.Equal(firstResult.UsageEventId, secondResult.UsageEventId);
        Assert.Equal(firstResult.Balance, secondResult.Balance);
        Assert.Equal(1, await testContext.DbContext.UsageEvents.CountAsync(usageEvent => usageEvent.AccountId == accountId));
        Assert.Equal(2, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId));
    }

    private static CreditLedgerEntry BuildLedgerEntry(Guid accountId, int amount, string idempotencyKey, CreditLedgerEntryType type)
    {
        return new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Type = type,
            Source = CreditLedgerEntrySource.ManualAdjustment,
            IdempotencyKey = idempotencyKey,
            ReferenceId = Guid.NewGuid().ToString(),
            Description = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<TestDbContext> CreateTestContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CreditFlowDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new CreditFlowDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return new TestDbContext(dbContext, connection);
    }

    private sealed class TestDbContext : IAsyncDisposable
    {
        public TestDbContext(CreditFlowDbContext dbContext, SqliteConnection connection)
        {
            DbContext = dbContext;
            Connection = connection;
        }

        public CreditFlowDbContext DbContext { get; }
        private SqliteConnection Connection { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}