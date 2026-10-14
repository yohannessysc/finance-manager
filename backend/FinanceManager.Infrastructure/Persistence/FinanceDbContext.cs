
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
        DbContextOptions<FinanceDbContext> options
    ) : base(options)
    {
    }

    // ---------------------------------------
    // Financial Accounts
    // ---------------------------------------

    public DbSet<FinancialAccount> FinancialAccounts =>
        Set<FinancialAccount>();

    // ---------------------------------------
    // Entity Configuration
    // ---------------------------------------

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // Required to configure ASP.NET Core Identity tables
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------
        // FinancialAccount Configuration
        // ---------------------------------------

        modelBuilder.Entity<FinancialAccount>(entity =>
        {
            // Primary key
            entity.HasKey(x => x.Id);

            // Ownership
            entity.Property(x => x.UserId)
                .IsRequired();

            // Account name
            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            // Account type
            entity.Property(x => x.AccountType)
                .IsRequired()
                .HasMaxLength(50);

            // Currency
            entity.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(3);

            // Opening balance
            entity.Property(x => x.OpeningBalance)
                .HasPrecision(18, 2);

            // Created timestamp
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            // Index for faster user-scoped account queries
            entity.HasIndex(x => x.UserId);

            // Relationship to ASP.NET Core Identity user
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------
        // ApplicationUser Configuration
        // ---------------------------------------

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
        });
    }
}
