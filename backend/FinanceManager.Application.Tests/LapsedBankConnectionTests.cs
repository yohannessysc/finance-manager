using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;
using FinanceManager.Infrastructure.Services.Plaid;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace FinanceManager.Application.Tests;

public sealed class LapsedBankConnectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly Mock<IPlaidService> _plaid = new();
    private readonly Mock<IEntitlementService> _entitlements = new();
    private readonly BankConnectionService _service;
    private readonly BankConnection _bank;

    public LapsedBankConnectionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "lapsed@example.com",
            Email = "lapsed@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _bank = new BankConnection
        {
            UserId = user.Id,
            PlaidAccessToken = "protected-token",
            PlaidItemId = "item-1",
            InstitutionName = "Test bank",
            InstitutionId = "ins_1",
            Status = (int)BankConnectionStatus.Connected
        };
        _db.Users.Add(user);
        _db.BankConnections.Add(_bank);
        _db.SaveChanges();

        var tokens = new Mock<IBankTokenProtector>();
        tokens.Setup(x => x.Unprotect(It.IsAny<string>())).Returns<string>(x => x);

        _service = new BankConnectionService(
            _db,
            _plaid.Object,
            tokens.Object,
            _entitlements.Object,
            NullLogger<BankConnectionService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SetPlan(SubscriptionPlan plan) =>
        _entitlements
            .Setup(x => x.GetPlanAsync(_bank.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

    [Fact]
    public async Task DowngradeStartsTheGracePeriodWithoutRemoving()
    {
        SetPlan(SubscriptionPlan.Free);

        var removed = await _service.RemoveLapsedConnectionsAsync();

        Assert.Equal(0, removed);
        Assert.NotNull(_bank.PlanLapsedAtUtc);
        Assert.False(_bank.IsDeleted);
        _plaid.Verify(x => x.RemoveItemAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConnectionIsRemovedAtPlaidAfterTheGracePeriod()
    {
        SetPlan(SubscriptionPlan.Free);
        _bank.PlanLapsedAtUtc = DateTime.UtcNow
            - BankConnectionService.LapsedConnectionGracePeriod
            - TimeSpan.FromMinutes(1);
        _db.SaveChanges();

        var removed = await _service.RemoveLapsedConnectionsAsync();

        Assert.Equal(1, removed);
        Assert.True(_bank.IsDeleted);
        Assert.Null(_bank.PlaidAccessToken);
        _plaid.Verify(x => x.RemoveItemAsync("protected-token"), Times.Once);
    }

    [Fact]
    public async Task UpgradingAgainCancelsTheRemoval()
    {
        SetPlan(SubscriptionPlan.Bank);
        _bank.PlanLapsedAtUtc = DateTime.UtcNow.AddDays(-40);
        _db.SaveChanges();

        var removed = await _service.RemoveLapsedConnectionsAsync();

        Assert.Equal(0, removed);
        Assert.Null(_bank.PlanLapsedAtUtc);
        Assert.False(_bank.IsDeleted);
    }
}
