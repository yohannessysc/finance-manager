using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class DashboardForecastTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly DashboardService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public DashboardForecastTests()
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
            UserName = "forecast@example.com",
            NormalizedUserName = "FORECAST@EXAMPLE.COM",
            Email = "forecast@example.com",
            NormalizedEmail = "FORECAST@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.UserPreferences.Add(new UserPreference
        {
            UserId = _userId,
            TimeZone = "UTC"
        });
        _db.SaveChanges();

        _service = new DashboardService(
            _db,
            new AccountBalanceService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetForecast_ProjectsFutureTransactionsAndRecurringActivity()
    {
        var now = DateTime.UtcNow;
        var account = new FinancialAccount
        {
            UserId = _userId,
            Name = "Checking",
            Currency = "CAD",
            OpeningBalance = 1_000
        };
        _db.FinancialAccounts.Add(account);
        _db.FinancialTransactions.AddRange(
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = account.Id,
                Type = TransactionType.Expense,
                Amount = 50,
                Description = "Past expense",
                TransactionDateUtc = now.AddDays(-1)
            },
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = account.Id,
                Type = TransactionType.Income,
                Amount = 200,
                Description = "Future income",
                TransactionDateUtc = now.AddDays(3)
            },
            new FinancialTransaction
            {
                UserId = _userId,
                FinancialAccountId = account.Id,
                Type = TransactionType.Expense,
                Amount = 25,
                Description = "Future expense",
                TransactionDateUtc = now.AddDays(2)
            });
        var recurringStart = now.AddDays(4);
        _db.RecurringTransactions.Add(new RecurringTransaction
        {
            UserId = _userId,
            FinancialAccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 40,
            Description = "Weekly bill",
            Frequency = RecurrenceFrequency.Weekly,
            StartDateUtc = recurringStart,
            NextOccurrenceUtc = recurringStart,
            TimeZoneId = "UTC"
        });
        await _db.SaveChangesAsync();

        var forecast = await _service.GetForecastAsync(_userId, 14);

        var currency = Assert.Single(forecast.Currencies);
        Assert.Equal(950m, currency.StartingBalance);
        Assert.Equal(1_045m, currency.ProjectedEndingBalance);
        Assert.Equal(14, currency.Days.Count);
        Assert.Equal(200m, currency.Days.Sum(x => x.ExpectedIncome));
        Assert.Equal(105m, currency.Days.Sum(x => x.ExpectedExpenses));
    }

    [Fact]
    public async Task GetForecast_RejectsHorizonOutsideSupportedRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.GetForecastAsync(_userId, 366));
    }
}
