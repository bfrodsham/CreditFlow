using CreditFlow.Domain;

namespace CreditFlow.Infrastructure;

public static class CreditFlowSeedData
{
    public static readonly Guid AccountAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AccountBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AccountCId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static IEnumerable<Account> Accounts()
    {
        return
        [
            new Account
            {
                Id = AccountAId,
                Name = "Acme Labs",
                Email = "billing@acmelabs.example",
                CreatedAt = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)
            },
            new Account
            {
                Id = AccountBId,
                Name = "Northwind Analytics",
                Email = "ops@northwind.example",
                CreatedAt = new DateTimeOffset(2026, 2, 4, 0, 0, 0, TimeSpan.Zero)
            },
            new Account
            {
                Id = AccountCId,
                Name = "Bluebird Systems",
                Email = "finance@bluebird.example",
                CreatedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)
            }
        ];
    }

    public static IEnumerable<CreditLedgerEntry> LedgerEntries()
    {
        return
        [
            new CreditLedgerEntry
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                AccountId = AccountAId,
                Amount = 300,
                Type = CreditLedgerEntryType.Grant,
                Source = CreditLedgerEntrySource.SubscriptionRenewal,
                IdempotencyKey = "seed-acme-grant-1",
                ReferenceId = "seed-acme-sub-1",
                Description = "Initial monthly grant",
                CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new CreditLedgerEntry
            {
                Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                AccountId = AccountAId,
                Amount = -80,
                Type = CreditLedgerEntryType.Consume,
                Source = CreditLedgerEntrySource.UsageEvent,
                IdempotencyKey = "seed-acme-use-1",
                ReferenceId = "seed-acme-event-1",
                Description = "Model inference usage",
                CreatedAt = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero)
            },
            new CreditLedgerEntry
            {
                Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
                AccountId = AccountBId,
                Amount = 100,
                Type = CreditLedgerEntryType.Grant,
                Source = CreditLedgerEntrySource.CreditPackPurchase,
                IdempotencyKey = "seed-nw-pack-1",
                ReferenceId = "seed-nw-packref-1",
                Description = "Starter credit pack",
                CreatedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero)
            },
            new CreditLedgerEntry
            {
                Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"),
                AccountId = AccountBId,
                Amount = -20,
                Type = CreditLedgerEntryType.Consume,
                Source = CreditLedgerEntrySource.UsageEvent,
                IdempotencyKey = "seed-nw-use-1",
                ReferenceId = "seed-nw-event-1",
                Description = "API usage",
                CreatedAt = new DateTimeOffset(2026, 6, 6, 9, 30, 0, TimeSpan.Zero)
            },
            new CreditLedgerEntry
            {
                Id = Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
                AccountId = AccountCId,
                Amount = 500,
                Type = CreditLedgerEntryType.Grant,
                Source = CreditLedgerEntrySource.SubscriptionRenewal,
                IdempotencyKey = "seed-bluebird-grant-1",
                ReferenceId = "seed-bluebird-sub-1",
                Description = "Enterprise monthly allowance",
                CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new CreditLedgerEntry
            {
                Id = Guid.Parse("cccccccc-0000-0000-0000-000000000002"),
                AccountId = AccountCId,
                Amount = 40,
                Type = CreditLedgerEntryType.Adjustment,
                Source = CreditLedgerEntrySource.ManualAdjustment,
                IdempotencyKey = "seed-bluebird-adj-1",
                ReferenceId = "seed-bluebird-adjref-1",
                Description = "Promotional adjustment",
                CreatedAt = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero)
            }
        ];
    }
}
