using CreditFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Infrastructure;

public class CreditFlowDbContext : DbContext
{
    public CreditFlowDbContext(DbContextOptions<CreditFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Name).IsRequired().HasMaxLength(200);
            entity.Property(account => account.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(account => account.Email).IsUnique();

            entity.HasData(CreditFlowSeedData.Accounts());
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Name).IsRequired().HasMaxLength(100);
            entity.Property(plan => plan.MonthlyCreditAllowance).IsRequired();
            entity.Property(plan => plan.AllowsRollover).IsRequired();
            entity.Property(plan => plan.PricePerExtraCredit).IsRequired().HasPrecision(10, 2);
            entity.Property(plan => plan.CreatedAt).IsRequired();

            entity.HasIndex(plan => plan.Name).IsUnique();

            entity.HasData(CreditFlowSeedData.Plans());
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(subscription => subscription.Id);
            entity.Property(subscription => subscription.CurrentPeriodStart).IsRequired();
            entity.Property(subscription => subscription.CurrentPeriodEnd).IsRequired();
            entity.Property(subscription => subscription.Status).IsRequired().HasMaxLength(50);
            entity.Property(subscription => subscription.CreatedAt).IsRequired();
            entity.Property(subscription => subscription.UpdatedAt).IsRequired();

            entity.HasIndex(subscription => subscription.AccountId).IsUnique();
            entity.HasIndex(subscription => subscription.PlanId);

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(subscription => subscription.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Plan>()
                .WithMany()
                .HasForeignKey(subscription => subscription.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(CreditFlowSeedData.Subscriptions());
        });

        modelBuilder.Entity<CreditLedgerEntry>(entity =>
        {
            entity.ToTable("CreditLedgerEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Amount).IsRequired();
            entity.Property(entry => entry.Type).IsRequired();
            entity.Property(entry => entry.Source).IsRequired();
            entity.Property(entry => entry.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.Property(entry => entry.ReferenceId).IsRequired().HasMaxLength(200);
            entity.Property(entry => entry.Description).IsRequired().HasMaxLength(500);
            entity.Property(entry => entry.CreatedAt).IsRequired();

            entity.HasIndex(entry => entry.AccountId);
            entity.HasIndex(entry => new { entry.AccountId, entry.IdempotencyKey }).IsUnique();

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(entry => entry.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(CreditFlowSeedData.LedgerEntries());
        });

        modelBuilder.Entity<UsageEvent>(entity =>
        {
            entity.ToTable("UsageEvents");
            entity.HasKey(usageEvent => usageEvent.Id);
            entity.Property(usageEvent => usageEvent.EventType).IsRequired().HasMaxLength(100);
            entity.Property(usageEvent => usageEvent.CreditCost).IsRequired();
            entity.Property(usageEvent => usageEvent.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.Property(usageEvent => usageEvent.OccurredAt).IsRequired();

            entity.HasIndex(usageEvent => usageEvent.AccountId);
            entity.HasIndex(usageEvent => new { usageEvent.AccountId, usageEvent.IdempotencyKey }).IsUnique();

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(usageEvent => usageEvent.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
