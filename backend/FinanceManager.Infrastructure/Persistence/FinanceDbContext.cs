
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

public class FinanceDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    // Financial accounts
    public DbSet<FinancialAccount> FinancialAccounts
        => Set<FinancialAccount>();

    // Manual and automatically posted transactions
    public DbSet<FinancialTransaction> FinancialTransactions
        => Set<FinancialTransaction>();

    // Recurring payment schedules
    public DbSet<RecurringTransaction> RecurringTransactions
        => Set<RecurringTransaction>();

    // Income and expense categories
    public DbSet<TransactionCategory> TransactionCategories
        => Set<TransactionCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // FINANCIAL ACCOUNTS
        // ==========================================

        modelBuilder.Entity<FinancialAccount>(entity =>
        {
            entity.ToTable("FinancialAccounts");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.AccountType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.OpeningBalance)
                .HasPrecision(18, 2);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId);
        });

        // ==========================================
        // TRANSACTION CATEGORIES
        // ==========================================

        modelBuilder.Entity<TransactionCategory>(entity =>
        {
            entity.ToTable("TransactionCategories");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.Type,
                x.Name
            }).IsUnique();
        });

        // ==========================================
        // RECURRING TRANSACTIONS
        // ==========================================

        modelBuilder.Entity<RecurringTransaction>(entity =>
        {
            entity.ToTable("RecurringTransactions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.Frequency)
                .IsRequired();

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.StartDateUtc)
                .IsRequired();

            entity.Property(x => x.NextOccurrenceUtc)
                .IsRequired();

            entity.Property(x => x.EndDateUtc);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            // IANA time zone, for example America/Toronto.
            // Existing schedules default to UTC.
            entity.Property(x => x.TimeZoneId)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("Etc/UTC");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.FinancialAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.DestinationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TransactionCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.Status,
                x.NextOccurrenceUtc
            });
        });

        // ==========================================
        // FINANCIAL TRANSACTIONS
        // ==========================================

        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.ToTable("FinancialTransactions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.Merchant)
                .HasMaxLength(200);

            entity.Property(x => x.TransactionDateUtc)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.ScheduledOccurrenceUtc);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.FinancialAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.DestinationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TransactionCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<RecurringTransaction>()
                .WithMany()
                .HasForeignKey(x => x.RecurringTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.TransactionDateUtc
            });

            entity.HasIndex(x => new
            {
                x.RecurringTransactionId,
                x.ScheduledOccurrenceUtc
            })
            .IsUnique()
            .HasFilter(
                "\"RecurringTransactionId\" IS NOT NULL " +
                "AND \"ScheduledOccurrenceUtc\" IS NOT NULL");
        });
    }
}
