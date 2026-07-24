using CreditFlow.Application;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Tests;

public class PlanServiceTests
{
    [Fact]
    public async Task ProcessDueRenewalsAsync_ExpiresRemainingCredits_ForNonRolloverPlans()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodEnd = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        testContext.DbContext.Accounts.Add(BuildAccount(accountId, "acme@example.com"));
        testContext.DbContext.Plans.Add(BuildPlan(planId, "Starter Test", 100, allowsRollover: false));
        testContext.DbContext.Subscriptions.Add(BuildSubscription(accountId, planId, periodEnd.AddMonths(-1), periodEnd));
        testContext.DbContext.CreditLedgerEntries.AddRange(
            BuildLedgerEntry(accountId, 200, CreditLedgerEntryType.Grant, "grant-1"),
            BuildLedgerEntry(accountId, -50, CreditLedgerEntryType.Consume, "consume-1"));

        await testContext.DbContext.SaveChangesAsync();

        var sut = new PlanService(testContext.DbContext);

        var renewedCount = await sut.ProcessDueRenewalsAsync(periodEnd.AddDays(1));

        Assert.Equal(1, renewedCount);

        var expireEntry = await testContext.DbContext.CreditLedgerEntries
            .SingleAsync(entry => entry.AccountId == accountId && entry.Type == CreditLedgerEntryType.Expire);
        Assert.Equal(-150, expireEntry.Amount);

        var grantEntry = await testContext.DbContext.CreditLedgerEntries
            .SingleAsync(entry => entry.AccountId == accountId && entry.Type == CreditLedgerEntryType.Grant && entry.IdempotencyKey.StartsWith("renewal-grant-"));
        Assert.Equal(100, grantEntry.Amount);

        var resultingBalance = await CreditLedgerService.GetBalanceAsync(testContext.DbContext.CreditLedgerEntries, accountId);
        Assert.Equal(100, resultingBalance);
    }

    [Fact]
    public async Task ProcessDueRenewalsAsync_GrantsNewAllowance_OnRenewal()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodEnd = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

        testContext.DbContext.Accounts.Add(BuildAccount(accountId, "northwind@example.com"));
        testContext.DbContext.Plans.Add(BuildPlan(planId, "Growth Test", 250, allowsRollover: false));
        testContext.DbContext.Subscriptions.Add(BuildSubscription(accountId, planId, periodEnd.AddMonths(-1), periodEnd));
        await testContext.DbContext.SaveChangesAsync();

        var sut = new PlanService(testContext.DbContext);

        var renewedCount = await sut.ProcessDueRenewalsAsync(periodEnd.AddMinutes(1));

        Assert.Equal(1, renewedCount);

        var renewalGrantEntries = await testContext.DbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId && entry.Type == CreditLedgerEntryType.Grant && entry.IdempotencyKey.StartsWith("renewal-grant-"))
            .ToListAsync();

        Assert.Single(renewalGrantEntries);
        Assert.Equal(250, renewalGrantEntries[0].Amount);

        var expireEntries = await testContext.DbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId && entry.Type == CreditLedgerEntryType.Expire)
            .ToListAsync();
        Assert.Empty(expireEntries);
    }

    [Fact]
    public async Task ProcessDueRenewalsAsync_RolloverPlansDoNotExpireRemainingCredits()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodEnd = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        testContext.DbContext.Accounts.Add(BuildAccount(accountId, "bluebird@example.com"));
        testContext.DbContext.Plans.Add(BuildPlan(planId, "Enterprise Test", 300, allowsRollover: true));
        testContext.DbContext.Subscriptions.Add(BuildSubscription(accountId, planId, periodEnd.AddMonths(-1), periodEnd));
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 80, CreditLedgerEntryType.Grant, "grant-rollover-1"));
        await testContext.DbContext.SaveChangesAsync();

        var sut = new PlanService(testContext.DbContext);

        var renewedCount = await sut.ProcessDueRenewalsAsync(periodEnd.AddMinutes(1));

        Assert.Equal(1, renewedCount);

        var expireEntries = await testContext.DbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId && entry.Type == CreditLedgerEntryType.Expire)
            .ToListAsync();
        Assert.Empty(expireEntries);

        var resultingBalance = await CreditLedgerService.GetBalanceAsync(testContext.DbContext.CreditLedgerEntries, accountId);
        Assert.Equal(380, resultingBalance);
    }

    [Fact]
    public async Task ChangePlanAsync_UpdatesSubscriptionWithoutChangingLedgerHistory()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();
        var proPlanId = Guid.NewGuid();

        testContext.DbContext.Accounts.Add(BuildAccount(accountId, "contoso@example.com"));
        testContext.DbContext.Plans.AddRange(
            BuildPlan(freePlanId, "Free Test", 100, allowsRollover: false),
            BuildPlan(proPlanId, "Pro Test", 500, allowsRollover: false));
        testContext.DbContext.Subscriptions.Add(BuildSubscription(
            accountId,
            freePlanId,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        testContext.DbContext.CreditLedgerEntries.AddRange(
            BuildLedgerEntry(accountId, 100, CreditLedgerEntryType.Grant, "grant-history-1"),
            BuildLedgerEntry(accountId, -30, CreditLedgerEntryType.Consume, "consume-history-1"));

        await testContext.DbContext.SaveChangesAsync();

        var originalLedgerIds = await testContext.DbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId)
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Id)
            .ToListAsync();

        var sut = new PlanService(testContext.DbContext);

        await sut.ChangePlanAsync(accountId, proPlanId);
        await sut.ChangePlanAsync(accountId, freePlanId);

        var updatedSubscription = await testContext.DbContext.Subscriptions.SingleAsync(item => item.AccountId == accountId);
        Assert.Equal(freePlanId, updatedSubscription.PlanId);

        var resultingLedgerIds = await testContext.DbContext.CreditLedgerEntries
            .Where(entry => entry.AccountId == accountId)
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Id)
            .ToListAsync();

        Assert.Equal(originalLedgerIds, resultingLedgerIds);
    }

    private static Account BuildAccount(Guid accountId, string email)
    {
        return new Account
        {
            Id = accountId,
            Name = email.Split('@')[0],
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Plan BuildPlan(Guid planId, string name, int monthlyAllowance, bool allowsRollover)
    {
        return new Plan
        {
            Id = planId,
            Name = name,
            MonthlyCreditAllowance = monthlyAllowance,
            AllowsRollover = allowsRollover,
            PricePerExtraCredit = 0.10m,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Subscription BuildSubscription(Guid accountId, Guid planId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        return new Subscription
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            PlanId = planId,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static CreditLedgerEntry BuildLedgerEntry(Guid accountId, int amount, CreditLedgerEntryType type, string idempotencyKey)
    {
        return new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Type = type,
            Source = CreditLedgerEntrySource.SubscriptionRenewal,
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