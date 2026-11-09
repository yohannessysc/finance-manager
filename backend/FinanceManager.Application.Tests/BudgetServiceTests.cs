using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class BudgetServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly BudgetService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public BudgetServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FinanceDbContext(options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "budget@example.com",
            NormalizedUserName = "BUDGET@EXAMPLE.COM",
            Email = "budget@example.com",
            NormalizedEmail = "BUDGET@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        _db.SaveChanges();
        _service = new BudgetService(
            _db,
            new HouseholdAuthorizationService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAll_UsesPreferencePeriodAndMatchingCurrencyExpenses()
    {
        var now = DateTime.UtcNow;
        var period = BudgetCycleCalculator.GetCurrentPeriod(
            now,
            1,
            "UTC");
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();

        _db.FinancialAccounts.Add(new FinancialAccount
        {
            Id = accountId,
            UserId = _userId,
            Name = "Checking",
            Currency = "CAD"
        });

        _db.TransactionCategories.Add(new TransactionCategory
        {
            Id = categoryId,
            UserId = _userId,
            Name = "Food",
            Type = TransactionType.Expense
        });

        _db.UserPreferences.Add(new UserPreference
        {
            UserId = _userId,
            BudgetStartDay = 1,
            TimeZone = "UTC"
        });

        _db.MonthlyBudgets.Add(new MonthlyBudget
        {
            Id = budgetId,
            UserId = _userId,
            CategoryId = categoryId,
            Currency = "CAD",
            MonthlyLimit = 200
        });

        _db.FinancialTransactions.AddRange(
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = accountId,
                CategoryId = categoryId,
                Type = TransactionType.Expense,
                Amount = 55.25m,
                Description = "Groceries",
                TransactionDateUtc = period.StartUtc.AddHours(1)
            },
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = accountId,
                CategoryId = categoryId,
                Type = TransactionType.Income,
                Amount = 500,
                Description = "Pay",
                TransactionDateUtc = period.StartUtc.AddHours(2)
            },
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = accountId,
                CategoryId = categoryId,
                Type = TransactionType.Expense,
                Amount = 100,
                Description = "Previous cycle",
                TransactionDateUtc = period.StartUtc.AddTicks(-1)
            });

        await _db.SaveChangesAsync();

        var budgets = await _service.GetAllAsync(_userId);

        var budget = Assert.Single(budgets);
        Assert.Equal("Food", budget.CategoryName);
        Assert.Equal(55.25m, budget.AmountSpent);
        Assert.Equal(144.75m, budget.Remaining);
        Assert.Equal(27.63m, budget.PercentSpent);
        Assert.Equal(period.StartUtc, budget.PeriodStartUtc);
        Assert.Equal(period.EndUtc, budget.PeriodEndUtc);
        Assert.Equal(
            DateOnly.FromDateTime(period.StartUtc),
            budget.PeriodStartDate);
        Assert.Equal(
            DateOnly.FromDateTime(period.EndUtc.AddTicks(-1)),
            budget.PeriodEndDate);
    }

    [Fact]
    public async Task Create_RejectsBudgetLimitWithFractionBeyondCents()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(
                _userId,
                new(
                    Guid.NewGuid(),
                    "CAD",
                    10.001m)));
    }

    [Fact]
    public async Task Create_StoresBudgetAndRejectsDuplicateCategoryCurrency()
    {
        var account = new FinancialAccount
        {
            UserId = _userId,
            Name = "Checking",
            Currency = "CAD"
        };
        var category = new TransactionCategory
        {
            UserId = _userId,
            Name = "Food",
            Type = TransactionType.Expense
        };

        _db.FinancialAccounts.Add(account);
        _db.TransactionCategories.Add(category);
        await _db.SaveChangesAsync();

        var request = new FinanceManager.Application.DTOs.Budgets.BudgetUpsertRequest(
            category.Id,
            "cad",
            300);

        var created = await _service.CreateAsync(_userId, request);

        Assert.Equal(category.Id, created.CategoryId);
        Assert.Equal("CAD", created.Currency);
        Assert.Equal(300, created.MonthlyLimit);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(_userId, request));
    }

    [Fact]
    public async Task GetAll_IncludesSharedBudgetActivityAndMarksViewerReadOnly()
    {
        var viewerId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser
        {
            Id = viewerId,
            UserName = "viewer@example.com",
            NormalizedUserName = "VIEWER@EXAMPLE.COM",
            Email = "viewer@example.com",
            NormalizedEmail = "VIEWER@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        var household = new Household
        {
            OwnerUserId = _userId,
            Name = "Home"
        };
        var account = new FinancialAccount
        {
            UserId = _userId,
            HouseholdId = household.Id,
            Name = "Shared checking",
            Currency = "CAD"
        };
        var category = new TransactionCategory
        {
            UserId = _userId,
            Name = "Food",
            Type = TransactionType.Expense
        };
        _db.Households.Add(household);
        _db.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = _userId,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = viewerId,
                Role = HouseholdRole.Viewer
            });
        _db.FinancialAccounts.Add(account);
        _db.TransactionCategories.Add(category);
        _db.UserPreferences.Add(new UserPreference
        {
            UserId = _userId,
            TimeZone = "UTC"
        });
        var period = BudgetCycleCalculator.GetCurrentPeriod(
            DateTime.UtcNow,
            1,
            "UTC");
        _db.MonthlyBudgets.Add(new MonthlyBudget
        {
            UserId = _userId,
            HouseholdId = household.Id,
            CategoryId = category.Id,
            Currency = "CAD",
            MonthlyLimit = 500
        });
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            UserId = viewerId,
            FinancialAccountId = account.Id,
            CategoryId = category.Id,
            Type = TransactionType.Expense,
            Amount = 75,
            Description = "Shared groceries",
            TransactionDateUtc = period.StartUtc.AddDays(1)
        });
        await _db.SaveChangesAsync();

        var ownerBudgets = await _service.GetAllAsync(_userId);
        var viewerBudgets = await _service.GetAllAsync(viewerId);

        var ownerBudget = Assert.Single(ownerBudgets);
        var viewerBudget = Assert.Single(viewerBudgets);
        Assert.Equal(household.Id, viewerBudget.HouseholdId);
        Assert.Equal(75m, viewerBudget.AmountSpent);
        Assert.True(ownerBudget.CanEdit);
        Assert.False(viewerBudget.CanEdit);
    }
}
