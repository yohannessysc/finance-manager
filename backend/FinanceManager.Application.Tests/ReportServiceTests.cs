using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class ReportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly ReportService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _cad = Guid.NewGuid();
    private readonly Guid _usd = Guid.NewGuid();
    private readonly DateOnly _thisMonth;
    private readonly DateOnly _lastMonth;

    public ReportServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "report@example.com",
            Email = "report@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.UserPreferences.Add(new UserPreference { UserId = _userId, TimeZone = "UTC" });
        _db.FinancialAccounts.AddRange(
            new FinancialAccount { Id = _cad, UserId = _userId, Name = "Chequing", Currency = "CAD" },
            new FinancialAccount { Id = _usd, UserId = _userId, Name = "US", Currency = "USD" });
        _db.SaveChanges();

        var now = DateTime.UtcNow;
        _thisMonth = new DateOnly(now.Year, now.Month, 1);
        _lastMonth = _thisMonth.AddMonths(-1);
        _service = new ReportService(_db, new HouseholdAuthorizationService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private TransactionCategory Category(string name, TransactionType type, Guid? parentId = null)
    {
        var category = new TransactionCategory { UserId = _userId, Name = name, Type = type, ParentCategoryId = parentId };
        _db.TransactionCategories.Add(category);
        return category;
    }

    private FinancialTransaction Add(Guid account, TransactionType type, decimal amount, Guid? categoryId, DateOnly month)
    {
        var transaction = new FinancialTransaction
        {
            UserId = _userId,
            FinancialAccountId = account,
            Type = type,
            Amount = amount,
            CategoryId = categoryId,
            Description = "x",
            TransactionDateUtc = month.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };
        _db.FinancialTransactions.Add(transaction);
        return transaction;
    }

    [Fact]
    public async Task GroupsByMonthAndRollsSubcategoriesIntoTheirParent()
    {
        var salary = Category("Salary", TransactionType.Income);
        var food = Category("Food", TransactionType.Expense);
        var groceries = Category("Groceries", TransactionType.Expense, food.Id);
        _db.SaveChanges();

        Add(_cad, TransactionType.Income, 3000m, salary.Id, _lastMonth);
        Add(_cad, TransactionType.Expense, 100m, groceries.Id, _lastMonth);
        Add(_cad, TransactionType.Expense, 40m, food.Id, _thisMonth);
        Add(_cad, TransactionType.Expense, 25m, null, _thisMonth);
        Add(_usd, TransactionType.Expense, 999m, food.Id, _thisMonth);
        _db.SaveChanges();

        var report = await _service.GetCategoryReportAsync(_userId, _lastMonth, _thisMonth, null);

        Assert.Equal("CAD", report.Currency);
        Assert.Equal(new[] { "CAD", "USD" }, report.Currencies);
        Assert.Equal(new[] { 3000m, 0m }, report.IncomeByMonth);
        Assert.Equal(new[] { 100m, 65m }, report.ExpensesByMonth);

        var foodRow = report.Categories.Single(x => x.CategoryId == food.Id);
        Assert.Equal(140m, foodRow.Total);
        Assert.Equal(new[] { 100m, 40m }, foodRow.ByMonth);
        Assert.Equal(100m, foodRow.Children.Single(x => x.CategoryId == groceries.Id).Total);
        Assert.Equal("General", foodRow.Children.Single(x => x.CategoryId == food.Id).Name);

        Assert.Equal(25m, report.Categories.Single(x => x.CategoryId == null).Total);
    }

    [Fact]
    public async Task SplitLinesAreReportedUnderTheirOwnCategories()
    {
        var groceries = Category("Groceries", TransactionType.Expense);
        var household = Category("Household", TransactionType.Expense);
        _db.SaveChanges();

        var costco = Add(_cad, TransactionType.Expense, 100m, null, _thisMonth);
        _db.SaveChanges();
        _db.TransactionSplits.AddRange(
            new TransactionSplit { TransactionId = costco.Id, CategoryId = groceries.Id, Amount = 70m },
            new TransactionSplit { TransactionId = costco.Id, CategoryId = household.Id, Amount = 30m });
        _db.SaveChanges();

        var report = await _service.GetCategoryReportAsync(_userId, _thisMonth, _thisMonth, "cad");

        Assert.Equal(70m, report.Categories.Single(x => x.CategoryId == groceries.Id).Total);
        Assert.Equal(30m, report.Categories.Single(x => x.CategoryId == household.Id).Total);
        Assert.DoesNotContain(report.Categories, x => x.CategoryId == null);
    }

    [Fact]
    public async Task RejectsRangesThatAreBackwardsOrTooLong()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetCategoryReportAsync(_userId, _thisMonth, _lastMonth, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetCategoryReportAsync(_userId, _thisMonth.AddMonths(-40), _thisMonth, null));
    }
}
