
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

public class FinanceDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>,
      IDataProtectionKeyContext
{
    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    // ASP.NET Data Protection keys, shared by every app instance so
    // auth cookies and encrypted bank tokens survive restarts.
    public DbSet<DataProtectionKey> DataProtectionKeys
        => Set<DataProtectionKey>();

    // Stripe subscription mirror, one row per paying (or once paying) user
    public DbSet<UserSubscription> UserSubscriptions
        => Set<UserSubscription>();

    // Financial accounts
    public DbSet<FinancialAccount> FinancialAccounts
        => Set<FinancialAccount>();

    // Manual and automatically posted transactions
    public DbSet<FinancialTransaction> FinancialTransactions
        => Set<FinancialTransaction>();

    // Category lines of split transactions
    public DbSet<TransactionSplit> TransactionSplits
        => Set<TransactionSplit>();

    // Receipts attached to transactions
    public DbSet<TransactionAttachment> TransactionAttachments
        => Set<TransactionAttachment>();

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

    // Bank integration
    public DbSet<BankConnection> BankConnections
        => Set<BankConnection>();

    public DbSet<BankTransaction> BankTransactions
        => Set<BankTransaction>();

    public DbSet<BankSyncLog> BankSyncLogs
        => Set<BankSyncLog>();

    public DbSet<CategoryRule> CategoryRules
        => Set<CategoryRule>();

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

            entity.Property(x => x.ExternalAccountId)
                .HasMaxLength(100);

            entity.Property(x => x.Mask)
                .HasMaxLength(10);

            // One app account per bank account per connection
            entity.HasIndex(x => new { x.BankConnectionId, x.ExternalAccountId })
                .IsUnique()
                .HasFilter("\"ExternalAccountId\" IS NOT NULL");

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

            entity.HasOne<TransactionCategory>()
                .WithMany()
                .HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // RECURRING TRANSACTIONS
        // ==========================================

        modelBuilder.Entity<RecurringTransaction>(entity =>
        {
            entity.ToTable("RecurringTransactions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ExternalStreamId)
                .HasMaxLength(100);

            entity.HasIndex(x => new { x.UserId, x.ExternalStreamId })
                .IsUnique()
                .HasFilter("\"ExternalStreamId\" IS NOT NULL");

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

            modelBuilder.Entity<TransactionSplit>(split =>
            {
                split.ToTable("TransactionSplits");
                split.HasKey(x => x.Id);
                split.Property(x => x.Amount)
                    .HasPrecision(18, 2)
                    .IsRequired();
                split.Property(x => x.Note)
                    .HasMaxLength(200);
                split.HasOne<FinancialTransaction>()
                    .WithMany()
                    .HasForeignKey(x => x.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);
                split.HasOne<TransactionCategory>()
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
                split.HasIndex(x => x.TransactionId);
            });

            modelBuilder.Entity<TransactionAttachment>(attachment =>
            {
                attachment.ToTable("TransactionAttachments");
                attachment.HasKey(x => x.Id);
                attachment.Property(x => x.FileName)
                    .IsRequired()
                    .HasMaxLength(200);
                attachment.Property(x => x.ContentType)
                    .IsRequired()
                    .HasMaxLength(100);
                attachment.Property(x => x.Data)
                    .IsRequired();
                attachment.HasOne<FinancialTransaction>()
                    .WithMany()
                    .HasForeignKey(x => x.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);
                attachment.HasIndex(x => x.TransactionId);
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

            // Existing rows are backfilled in the migration. No model
            // default: EF would swap an explicit false/0 for the default.
            entity.Property(x => x.GoalType)
                .IsRequired();

            entity.Property(x => x.AutoContributionAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.AutoContributionStartUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.NextAutoContributionUtc)
                .HasColumnType("timestamp with time zone");

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.LinkedAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.NextAutoContributionUtc);

            // One goal per account, so a balance is never counted twice.
            entity.HasIndex(x => x.LinkedAccountId)
                .IsUnique();
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

            entity.Property(x => x.ScheduledOccurrenceUtc)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(x => new
            {
                x.UserId,
                x.SavingsGoalId,
                x.ContributionDateUtc
            });

            // Each scheduled occurrence posts at most once.
            entity.HasIndex(x => new
            {
                x.SavingsGoalId,
                x.ScheduledOccurrenceUtc
            })
                .IsUnique();
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.ToTable("UserSubscriptions");

            entity.HasKey(x => x.UserId);

            entity.Property(x => x.StripeCustomerId)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.StripeSubscriptionId)
                .HasMaxLength(255);

            entity.Property(x => x.Plan)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(40);

            entity.Property(x => x.CurrentPeriodEndUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Ignore(x => x.EffectivePlan);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.StripeCustomerId)
                .IsUnique();
        });

        // ==========================================
        // BANK INTEGRATION
        // ==========================================

        modelBuilder.Entity<BankConnection>(entity =>
        {
            entity.ToTable("BankConnections");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.PlaidAccessToken)
                .HasMaxLength(500);

            entity.Property(x => x.PlaidItemId)
                .HasMaxLength(100);

            // Plaid webhooks identify the connection by item id.
            entity.HasIndex(x => x.PlaidItemId);

            entity.Property(x => x.TransactionsCursor)
                .HasMaxLength(1000);

            entity.Property(x => x.FinicityInstitutionName)
                .HasMaxLength(200);

            entity.Property(x => x.FinicityCustomerId)
                .HasMaxLength(50);

            entity.Property(x => x.BankProvider)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.InstitutionName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.InstitutionId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.ConnectedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.LastSyncAtUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.ExpiresAtUtc)
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

            entity.HasOne<FinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.FinancialAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.LastSyncAtUtc);
            entity.HasIndex(x => x.IsDeleted);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.ToTable("BankTransactions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.BankTransactionId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.TransactionDateUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.Amount)
                .IsRequired()
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.Merchant)
                .HasMaxLength(200);

            entity.Property(x => x.AccountType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.MaskedAccountNumber)
                .HasMaxLength(10);

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.TransactionHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.AutoMatchedCategory)
                .HasMaxLength(150);

            entity.Property(x => x.ImportedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasOne(x => x.BankConnection)
                .WithMany(bc => bc.BankTransactions)
                .HasForeignKey(x => x.BankConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.FinancialTransaction)
                .WithMany()
                .HasForeignKey(x => x.FinancialTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.BankConnectionId);
            entity.HasIndex(x => x.TransactionHash);
            entity.HasIndex(x => x.TransactionDateUtc);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.FinancialTransactionId);
        });

        modelBuilder.Entity<BankSyncLog>(entity =>
        {
            entity.ToTable("BankSyncLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.StartedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.CompletedAtUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(500);

            entity.Property(x => x.SyncFromDateUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.SyncToDateUtc)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.DetailedLog)
                .HasColumnType("text");

            entity.HasOne(x => x.BankConnection)
                .WithMany(bc => bc.SyncLogs)
                .HasForeignKey(x => x.BankConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.BankConnectionId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartedAtUtc);
        });

        modelBuilder.Entity<CategoryRule>(entity =>
        {
            entity.ToTable("CategoryRules");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.MerchantPattern)
                .HasMaxLength(200);

            entity.Property(x => x.DescriptionPattern)
                .HasMaxLength(200);

            entity.Property(x => x.MinAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.MaxAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Priority)
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
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TransactionCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.IsActive);
        });
    }
}
