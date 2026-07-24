using CreditFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure;

public static class CreditFlowSeedData
{
    public static readonly Guid AccountAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AccountBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AccountCId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid FreePlanId = Guid.Parse("44444444-4444-4444-4444-444444444441");
    public static readonly Guid ProPlanId = Guid.Parse("44444444-4444-4444-4444-444444444442");
    public static readonly Guid EnterprisePlanId = Guid.Parse("44444444-4444-4444-4444-444444444443");
    public static readonly Guid AccountASubscriptionId = Guid.Parse("55555555-5555-5555-5555-555555555551");
    public static readonly Guid AccountBSubscriptionId = Guid.Parse("55555555-5555-5555-5555-555555555552");
    public static readonly Guid AccountCSubscriptionId = Guid.Parse("55555555-5555-5555-5555-555555555553");

    private static readonly DateTimeOffset LegacySeedPeriodStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LegacySeedPeriodEnd = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

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

    public static IEnumerable<Plan> Plans()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return
        [
            new Plan
            {
                Id = FreePlanId,
                Name = "Free",
                MonthlyCreditAllowance = 100,
                AllowsRollover = false,
                PricePerExtraCredit = 0.20m,
                CreatedAt = createdAt
            },
            new Plan
            {
                Id = ProPlanId,
                Name = "Pro",
                MonthlyCreditAllowance = 500,
                AllowsRollover = false,
                PricePerExtraCredit = 0.10m,
                CreatedAt = createdAt
            },
            new Plan
            {
                Id = EnterprisePlanId,
                Name = "Enterprise",
                MonthlyCreditAllowance = 1200,
                AllowsRollover = true,
                PricePerExtraCredit = 0.05m,
                CreatedAt = createdAt
            }
        ];
    }

    public static IEnumerable<Subscription> Subscriptions()
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);
        var createdAt = periodStart;

        return
        [
            new Subscription
            {
                Id = AccountASubscriptionId,
                AccountId = AccountAId,
                PlanId = FreePlanId,
                CurrentPeriodStart = periodStart,
                CurrentPeriodEnd = periodEnd,
                Status = "Active",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Subscription
            {
                Id = AccountBSubscriptionId,
                AccountId = AccountBId,
                PlanId = ProPlanId,
                CurrentPeriodStart = periodStart,
                CurrentPeriodEnd = periodEnd,
                Status = "Active",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Subscription
            {
                Id = AccountCSubscriptionId,
                AccountId = AccountCId,
                PlanId = EnterprisePlanId,
                CurrentPeriodStart = periodStart,
                CurrentPeriodEnd = periodEnd,
                Status = "Active",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            }
        ];
    }

    public static async Task AlignSeedSubscriptionsToCurrentMonthAsync(CreditFlowDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);

        var seedSubscriptionIds = new[]
        {
            AccountASubscriptionId,
            AccountBSubscriptionId,
            AccountCSubscriptionId
        };

        var subscriptions = await dbContext.Subscriptions
            .Where(subscription => seedSubscriptionIds.Contains(subscription.Id))
            .ToListAsync(cancellationToken);

        var hasChanges = false;
        foreach (var subscription in subscriptions)
        {
            var isLegacySeedWindow = subscription.CurrentPeriodStart == LegacySeedPeriodStart
                && subscription.CurrentPeriodEnd == LegacySeedPeriodEnd;

            if (!isLegacySeedWindow)
            {
                continue;
            }

            subscription.CurrentPeriodStart = periodStart;
            subscription.CurrentPeriodEnd = periodEnd;
            subscription.UpdatedAt = now;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
