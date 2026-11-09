
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

    // Persistent per-account email cooldowns
    public DbSet<EmailSendCooldown> EmailSendCooldowns
        => Set<EmailSendCooldown>();

    // Per-user display and budgeting preferences
    public DbSet<UserPreference> UserPreferences
        => Set<UserPreference>();

    // Server-side login sessions
    public DbSet<UserSession> UserSessions
        => Set<UserSession>();

    public DbSet<MonthlyBudget> MonthlyBudgets
        => Set<MonthlyBudget>();

    public DbSet<SavingsGoal> SavingsGoals
        => Set<SavingsGoal>();

    public DbSet<SavingsGoalContribution> SavingsGoalContributions
        => Set<SavingsGoalContribution>();

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> HouseholdMembers
        => Set<HouseholdMember>();

    public DbSet<HouseholdInvitation> HouseholdInvitations
        => Set<HouseholdInvitation>();

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

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.SetNull);
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

            entity.Property(x => x.ImportFingerprint)
                .HasMaxLength(64);

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

            modelBuilder.Entity<Household>(entity =>
            {
                entity.ToTable("Households");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(x => x.CreatedAtUtc)
                    .IsRequired()
                    .HasColumnType("timestamp with time zone");
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(x => x.OwnerUserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(x => x.OwnerUserId);
            });

            modelBuilder.Entity<HouseholdMember>(entity =>
            {
                entity.ToTable("HouseholdMembers");
                entity.HasKey(x => new { x.HouseholdId, x.UserId });
                entity.Property(x => x.Role).IsRequired();
                entity.Property(x => x.JoinedAtUtc)
                    .IsRequired()
                    .HasColumnType("timestamp with time zone");
                entity.HasOne<Household>()
                    .WithMany()
                    .HasForeignKey(x => x.HouseholdId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(x => new { x.UserId, x.HouseholdId });
            });

            modelBuilder.Entity<HouseholdInvitation>(entity =>
            {
                entity.ToTable("HouseholdInvitations");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Email)
                    .IsRequired()
                    .HasMaxLength(256);
                entity.Property(x => x.NormalizedEmail)
                    .IsRequired()
                    .HasMaxLength(256);
                entity.Property(x => x.Role).IsRequired();
                entity.Property(x => x.TokenHash)
                    .IsRequired()
                    .HasMaxLength(64);
                entity.Property(x => x.CreatedAtUtc)
                    .IsRequired()
                    .HasColumnType("timestamp with time zone");
                entity.Property(x => x.ExpiresAtUtc)
                    .IsRequired()
                    .HasColumnType("timestamp with time zone");
                entity.Property(x => x.AcceptedAtUtc)
                    .HasColumnType("timestamp with time zone");
                entity.HasOne<Household>()
                    .WithMany()
                    .HasForeignKey(x => x.HouseholdId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(x => x.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasIndex(x => new
                {
                    x.HouseholdId,
                    x.NormalizedEmail,
                    x.ExpiresAtUtc
                });
            });

            entity.HasIndex(x => new
            {
                x.UserId,
                x.ImportFingerprint
            })
            .IsUnique()
            .HasFilter("\"ImportFingerprint\" IS NOT NULL");

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

        // ==========================================
        // EMAIL SEND COOLDOWNS
        // ==========================================

        modelBuilder.Entity<EmailSendCooldown>(entity =>
        {
            entity.ToTable("EmailSendCooldowns");

            // One record per user and email purpose.
            entity.HasKey(x => new
            {
                x.UserId,
                x.Purpose
            });

            entity.Property(x => x.Purpose)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.LastSentAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("UserPreferences");

            entity.HasKey(x => x.UserId);

            entity.Property(x => x.DefaultCurrency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.TimeZone)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.DateFormat)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(x => x.BudgetStartDay)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserPreference>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("UserSessions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.LastSeenAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.ExpiresAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.RevokedAtUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UserAgent)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.DeviceName)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<MonthlyBudget>(entity =>
        {
            entity.ToTable("MonthlyBudgets");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.MonthlyLimit)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TransactionCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.CategoryId,
                x.Currency
            })
            .IsUnique()
            .HasFilter("\"HouseholdId\" IS NULL");

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.CategoryId,
                x.Currency
            })
            .IsUnique()
            .HasFilter("\"HouseholdId\" IS NOT NULL");
        });

        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.ToTable("SavingsGoals");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.TargetAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.TargetDateUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<SavingsGoalContribution>(entity =>
        {
            entity.ToTable("SavingsGoalContributions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.ContributionDateUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.Note)
                .HasMaxLength(250);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<SavingsGoal>()
                .WithMany()
                .HasForeignKey(x => x.SavingsGoalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.SavingsGoalId,
                x.ContributionDateUtc
            });
        });
    }
}
