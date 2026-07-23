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
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();

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
    }
}
