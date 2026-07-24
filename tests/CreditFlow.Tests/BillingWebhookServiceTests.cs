using CreditFlow.Application;
using CreditFlow.Contracts;
using CreditFlow.Domain;
using CreditFlow.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreditFlow.Tests;

public class BillingWebhookServiceTests
{
    private const string SigningSecret = "test-webhook-secret";

    [Fact]
    public async Task ProcessPaymentEvent_ValidSignature_GrantsCredits()
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
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 50, "seed-grant"));
        await testContext.DbContext.SaveChangesAsync();

        var payload = CreatePayload(accountId, 120, "payment-1", "payment-ref-1");
        var signature = BillingWebhookService.CreateSignature(payload, SigningSecret);

        var sut = CreateSut(testContext.DbContext);

        var result = await sut.ProcessPaymentEvent(payload, signature);

        Assert.False(result.AlreadyProcessed);
        Assert.Equal(120, result.CreditsGranted);
        Assert.Equal(170, result.Balance);

        var ledgerEntry = await testContext.DbContext.CreditLedgerEntries.SingleAsync(entry => entry.IdempotencyKey == "payment-1");
        Assert.Equal(CreditLedgerEntryType.Grant, ledgerEntry.Type);
        Assert.Equal(CreditLedgerEntrySource.CreditPackPurchase, ledgerEntry.Source);
        Assert.Equal(120, ledgerEntry.Amount);
    }

    [Fact]
    public async Task ProcessPaymentEvent_InvalidSignature_RejectsWithoutWritingLedgerEntry()
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
        testContext.DbContext.CreditLedgerEntries.Add(BuildLedgerEntry(accountId, 25, "seed-grant"));
        await testContext.DbContext.SaveChangesAsync();

        var payload = CreatePayload(accountId, 100, "payment-2", "payment-ref-2");

        var sut = CreateSut(testContext.DbContext);

        await Assert.ThrowsAsync<InvalidWebhookSignatureException>(() => sut.ProcessPaymentEvent(payload, "sha256=deadbeef"));

        Assert.Equal(1, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId));
        Assert.Equal(0, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId && entry.IdempotencyKey == "payment-2"));
    }

    [Fact]
    public async Task ProcessPaymentEvent_DuplicateIdempotencyKey_DoesNotDoubleGrant()
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
        await testContext.DbContext.SaveChangesAsync();

        var payload = CreatePayload(accountId, 80, "payment-3", "payment-ref-3");
        var signature = BillingWebhookService.CreateSignature(payload, SigningSecret);

        var sut = CreateSut(testContext.DbContext);

        var firstResult = await sut.ProcessPaymentEvent(payload, signature);
        var secondResult = await sut.ProcessPaymentEvent(payload, signature);

        Assert.False(firstResult.AlreadyProcessed);
        Assert.True(secondResult.AlreadyProcessed);
        Assert.Equal(firstResult.Balance, secondResult.Balance);
        Assert.Equal(1, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId && entry.IdempotencyKey == "payment-3"));
    }

    [Fact]
    public async Task ProcessPaymentEvent_MalformedPayload_IsRejectedCleanly()
    {
        await using var testContext = await CreateTestContextAsync();
        var accountId = Guid.NewGuid();

        testContext.DbContext.Accounts.Add(new Account
        {
            Id = accountId,
            Name = "Contoso",
            Email = "contoso@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await testContext.DbContext.SaveChangesAsync();

        const string malformedPayload = "{\"accountId\":\"not-a-guid\",\"credits\":100,\"idempotencyKey\":\"payment-4\",\"referenceId\":\"ref-4\"}";
        var signature = BillingWebhookService.CreateSignature(malformedPayload, SigningSecret);

        var sut = CreateSut(testContext.DbContext);

        await Assert.ThrowsAsync<MalformedWebhookPayloadException>(() => sut.ProcessPaymentEvent(malformedPayload, signature));

        Assert.Equal(0, await testContext.DbContext.CreditLedgerEntries.CountAsync(entry => entry.AccountId == accountId && entry.IdempotencyKey == "payment-4"));
    }

    private static BillingWebhookService CreateSut(CreditFlowDbContext dbContext)
    {
        var options = Options.Create(new BillingWebhookOptions
        {
            SigningSecret = SigningSecret
        });

        return new BillingWebhookService(dbContext, options);
    }

    private static string CreatePayload(Guid accountId, int credits, string idempotencyKey, string referenceId)
    {
        var payload = new PaymentWebhookPayloadDto(accountId, credits, idempotencyKey, referenceId, "Payment received");
        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private static CreditLedgerEntry BuildLedgerEntry(Guid accountId, int amount, string idempotencyKey)
    {
        return new CreditLedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Type = CreditLedgerEntryType.Grant,
            Source = CreditLedgerEntrySource.ManualAdjustment,
            IdempotencyKey = idempotencyKey,
            ReferenceId = Guid.NewGuid().ToString(),
            Description = "seed",
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
