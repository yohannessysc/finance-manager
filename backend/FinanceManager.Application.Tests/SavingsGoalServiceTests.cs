using FinanceManager.Application.DTOs.SavingsGoals;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class SavingsGoalServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly SavingsGoalService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public SavingsGoalServiceTests()
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
            UserName = "goal@example.com",
            NormalizedUserName = "GOAL@EXAMPLE.COM",
            Email = "goal@example.com",
            NormalizedEmail = "GOAL@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        _db.FinancialAccounts.Add(new FinancialAccount
        {
            UserId = _userId,
            Name = "Savings",
            Currency = "CAD"
        });

        _db.SaveChanges();
        _service = new SavingsGoalService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ContributionsUpdateProgressAndCanBeRemoved()
    {
        var goal = await _service.CreateAsync(
            _userId,
            new SavingsGoalUpsertRequest(
                "Emergency fund",
                "cad",
                1000,
                DateTime.UtcNow.AddMonths(6)));

        var updated = await _service.AddContributionAsync(
            goal.Id,
            _userId,
            new SavingsGoalContributionRequest(
                125.50m,
                DateTime.UtcNow.Date.AddHours(12),
                "First deposit"));

        Assert.NotNull(updated);
        Assert.Equal(125.50m, updated.AmountSaved);
        Assert.Equal(874.50m, updated.Remaining);
        Assert.Equal(12.55m, updated.ProgressPercent);

        var contributions = await _service.GetContributionsAsync(
            goal.Id,
            _userId);
        Assert.NotNull(contributions);
        var contribution = Assert.Single(contributions);

        Assert.Equal("First deposit", contribution.Note);

        Assert.True(await _service.DeleteContributionAsync(
            goal.Id,
            contribution.Id,
            _userId));

        var reloaded = Assert.Single(await _service.GetAllAsync(_userId));
        Assert.Equal(0, reloaded.AmountSaved);
        Assert.Equal(1000, reloaded.Remaining);
    }

    [Fact]
    public async Task ContributionCannotBeAddedToAnotherUsersGoal()
    {
        var otherUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser
        {
            Id = otherUserId,
            UserName = "other@example.com",
            NormalizedUserName = "OTHER@EXAMPLE.COM",
            Email = "other@example.com",
            NormalizedEmail = "OTHER@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var otherAccount = new FinancialAccount
        {
            UserId = otherUserId,
            Name = "Other savings",
            Currency = "CAD"
        };
        _db.FinancialAccounts.Add(otherAccount);
        await _db.SaveChangesAsync();

        var otherService = new SavingsGoalService(_db);
        var goal = await otherService.CreateAsync(
            otherUserId,
            new SavingsGoalUpsertRequest(
                "Private goal",
                "CAD",
                100,
                null));

        var contribution = await _service.AddContributionAsync(
            goal.Id,
            _userId,
            new SavingsGoalContributionRequest(
                10,
                DateTime.UtcNow.Date,
                null));

        Assert.Null(contribution);
        Assert.Empty(await _service.GetAllAsync(_userId));
    }
}
